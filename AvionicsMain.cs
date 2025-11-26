﻿using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using StarMap.API;
using System;
using ModMenu;
using System.Text.Json;
using static Brutal.Numerics.PackUtils;

namespace Avionics
{
    [StarMapMod]
    public class AvionicsMain {
        List<Airport> airports;
        Airport selectedAirport;
        Runway selectedRunway;
        static ImInputString airport_text_buffer = new ImInputString(9);
        static ImInputString glideslope_text_buffer = new ImInputString(3, "3");
        static bool runwaySelectionPageOn;
        static bool hsiPageOn;

        [ModMenuEntry("Avionics")]
        public static void DrawMenu() {

            if(ImGui.BeginMenu("Unit")) {
                if(ImGui.MenuItem("Kilometers")) {
                    FlightInstruments.CurrentUnit = FlightInstruments.DistanceUnit.Kilometers;
                } else if(ImGui.MenuItem("Miles")) {
                    FlightInstruments.CurrentUnit = FlightInstruments.DistanceUnit.Miles;
                } else if(ImGui.MenuItem("Nautical Miles")) {
                    FlightInstruments.CurrentUnit = FlightInstruments.DistanceUnit.NauticalMiles;
                }
                ImGui.EndMenu();
            }
            if(ImGui.MenuItem("Runway selection")) {
                AvionicsMain.runwaySelectionPageOn = !AvionicsMain.runwaySelectionPageOn;
            }
            if(ImGui.MenuItem("HSI")) {
                AvionicsMain.hsiPageOn = !AvionicsMain.hsiPageOn;
            }
        }

        [StarMapImmediateLoad]
        public void Init(Mod definingMod) {
            // Load a list of airports from a file
            var json = File.ReadAllText("Content/Avionics/airports.json");
            var options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            };
            airports = JsonSerializer.Deserialize<List<Airport>>(json, options);
        }

        [StarMapAfterGui]
        public void OnAfterUi(double dt) {
            string entered = airport_text_buffer.ToString().Trim().ToUpper();
            selectedAirport = airports.Find(a => a.Ident.Equals(entered, StringComparison.OrdinalIgnoreCase));

            ImGuiWindowFlags flags = ImGuiWindowFlags.MenuBar;
            Vehicle controlledVehicle = Program.ControlledVehicle;
            if(controlledVehicle == null) {
                return;
            }
            double3 positionGPS = GetGPSPosition(controlledVehicle);
            string positionText = GetGPSPositionString(positionGPS);
            string bearingText = "No airport selected";
            string airportTargetGPSText = "No airport selected";
            string airportDistanceText = "No airport selected";
            string runwayTargetGPSText = "No runway selected";
            string runwayDistanceText = "No runway selected";
            double runwayBearing = 0d;
            double runwayDistance = 0d;
            string runwayTargetText = "No runway selected";
            string lateralDeviationText = "No runway selected";
            string verticalDeviationText = "No runway selected";
            double2 deviation = new double2(double.MaxValue, double.MaxValue);
            string angleText = "No runway selected";
            double slope = 0d;
            string swapRunwayDirectionButtonText = "No runway selected";
            string chooseARunwayMenuText = "No airport selected";


            double heading = GetHeading(controlledVehicle);
            string headingText = GetHeadingString(heading);

            if(selectedAirport != null) {
                double target_lat_rad = Deg2Rad(selectedAirport.Latitude_deg);
                double target_lon_rad = Deg2Rad(selectedAirport.longitude_deg);
                double3 targetGPS = new double3(target_lat_rad, target_lon_rad, 0);
                airportTargetGPSText = GetGPSPositionString(targetGPS);

                double bearing = GetBearing(positionGPS, targetGPS);
                bearingText = GetBearingString(bearing);

                double distance = GetDistance(positionGPS, targetGPS, controlledVehicle.Orbit.Parent.MeanRadius);
                airportDistanceText = FlightInstruments.DistanceToString((float)distance);
                if(selectedAirport.Runways.Count > 0) {
                    chooseARunwayMenuText = "Choose a runway";
                } else {
                    chooseARunwayMenuText = "No runways at this areodome";
                }
            }
            if(selectedRunway != null) {
                double3 runwayGPS = selectedRunway.GetGPS();
                runwayBearing = GetBearing(positionGPS, runwayGPS);
                runwayTargetGPSText = GetGPSPositionString(runwayGPS);
                runwayDistance = GetDistance(positionGPS, runwayGPS, controlledVehicle.Orbit.Parent.MeanRadius);
                runwayDistanceText = $"{runwayDistance:F2} m";
                runwayTargetText = selectedRunway.GetIdent();
                double lateralDeviation = selectedRunway.GetLateralDeviation(positionGPS, controlledVehicle.Orbit.Parent.MeanRadius);
                lateralDeviationText = FlightInstruments.RadToString((float)lateralDeviation);
                double verticalDeviation = selectedRunway.GetVerticalDeviation(positionGPS, controlledVehicle.Orbit.Parent.MeanRadius);
                verticalDeviationText = FlightInstruments.RadToString((float)verticalDeviation);
                deviation = new double2(lateralDeviation, verticalDeviation);
                slope = selectedRunway.GetCurrentVerticalAngle(positionGPS, controlledVehicle.Orbit.Parent.MeanRadius);
                angleText = FlightInstruments.RadToString((float)slope);
                swapRunwayDirectionButtonText = "Swap runway direction";

                string gs_string = glideslope_text_buffer.ToString();
                var sb = new System.Text.StringBuilder(gs_string.Length);
                foreach(char c in gs_string) {
                    if(char.IsDigit(c))
                        sb.Append(c);
                }

                // Handle empty result (no digits found)
                int gs;
                if(sb.Length == 0) {
                    gs = 3;
                } else {
                    gs = int.Parse(sb.ToString());

                }

                float gs_rad = (float)(gs * (Math.PI / 180.0));
                selectedRunway.glideSlope = (float)gs_rad;
                glideslope_text_buffer.SetValue(gs.ToString());
            }

            // Runway selection page
            if(AvionicsMain.runwaySelectionPageOn) {
                ImGui.Begin("Runway selection", ref AvionicsMain.runwaySelectionPageOn, flags);

                if(ImGui.BeginMenuBar()) {
                    if(ImGui.BeginMenu(chooseARunwayMenuText)) {
                        if(selectedAirport != null && selectedAirport.Runways.Count > 0) {
                            foreach(Runway runway in selectedAirport.Runways) {
                                if(ImGui.MenuItem(runway.GetDescription())) {
                                    selectedRunway = runway;
                                }
                            }
                        }
                        ImGui.EndMenu();
                    }
                    ImGui.EndMenuBar();
                }

                ImGui.Text("Enter airport ICAO");
                ImGui.InputText("ICAO", airport_text_buffer);

                if(ImGui.Button("Nearest Aerodome with runway")) {
                    selectedAirport = GetNearestAirport(positionGPS, airports);
                    airport_text_buffer.SetValue(selectedAirport.Ident);
                }
                if(ImGui.Button(swapRunwayDirectionButtonText)) {
                    if(selectedRunway != null) {
                        if(selectedRunway.Use_LE) {
                            selectedRunway.Use_LE = false;
                        } else {
                            selectedRunway.Use_LE = true;
                        }
                    }
                }
                ImGui.Text("Enter glideslope");
                ImGui.InputText("GS", glideslope_text_buffer);
                ImGui.Text($"Vehicle position: {positionText}");
                ImGui.Text($"Vehicle heading: {headingText}");
                ImGui.Text("");
                ImGui.Text($"Selected Airport: {selectedAirport?.Name ?? "None"}");
                ImGui.Text($"Airport position: {airportTargetGPSText}");
                ImGui.Text($"Bearing: {bearingText}");
                ImGui.Text($"Distance: {airportDistanceText}");
                ImGui.Text("");
                ImGui.Text($"Selected Runway: {runwayTargetText}");
                //ImGui.Text($"Runway position: {runwayTargetGPSText}");
                //ImGui.Text($"Runway distance: {runwayDistanceText}");
                ImGui.Text($"Lateral deviation: {lateralDeviationText}");
                ImGui.Text($"Vertical deviation: {verticalDeviationText}");
                ImGui.Text($"Rising angle: {angleText}");
                ImGui.End();
            }

            // HSI page
            if(AvionicsMain.hsiPageOn) {
                ImGui.SetNextWindowSizeConstraints(new float2(300,300), new float2(float.MaxValue, float.MaxValue));
                ImGui.Begin("HSI", ref AvionicsMain.hsiPageOn, flags);
                unsafe {
                    ImDrawList* draw_list = ImGui.GetWindowDrawList();
                    ImDrawList* draw_list1 = draw_list;
                    float2 center = ImGui.GetWindowPos();
                    float2 size = ImGui.GetWindowSize();

                    Astronomical parent = controlledVehicle.Orbit.Parent;
                    Celestial celestial = (Celestial)controlledVehicle.Orbit.Parent;


                    FlightInstruments.RenderHSI(draw_list, center, size, (float)heading, selectedRunway, deviation, (float)runwayBearing, (float)runwayDistance, (float)slope);
                }
                ImGui.End();
                ImGui.Render();
            }
        }
        // Return GPS position as (latitude [radians], longitude [radians], altitude [meters])
        public static double3 GetGPSPosition(Vehicle vehicle) {
            Astronomical parent = vehicle.Orbit.Parent;
            Celestial celestial = (Celestial)vehicle.Orbit.Parent;
            double3 surface_position = celestial.GetCci2Ccf() * vehicle.GetPositionCci();

            double x = surface_position[0];
            double y = surface_position[1];
            double z = surface_position[2];

            double r = Math.Sqrt(x * x + y * y + z * z);

            double latitude = Math.Asin(z / r);     // [-pi/2, +pi/2]
            double longitude = Math.Atan2(y, x);     // [-pi, +pi]
            double altitude = r - parent.MeanRadius;

            return new double3(latitude, longitude, altitude);
        }
        public static string GetGPSPositionString(double3 GPSPos) {
            // Convert to degrees
            double latitude_deg = GPSPos[0] * (180.0 / Math.PI);
            double longitude_deg = GPSPos[1] * (180.0 / Math.PI);

            // Convert to string and append N/S, E/W
            string lat_hemisphere = latitude_deg >= 0 ? "N" : "S";
            string lon_hemisphere = longitude_deg >= 0 ? "E" : "W";
            string lat_str = $"{Math.Abs(latitude_deg):F4}° {lat_hemisphere}";
            string lon_str = $"{Math.Abs(longitude_deg):F4}° {lon_hemisphere}";

            return $"{lat_str}, {lon_str}";
        }
        public static double GetBearing(double3 GPSPos1, double3 GPSPos2) {
            double lat1 = GPSPos1[0];
            double lon1 = GPSPos1[1];

            double lat2 = GPSPos2[0];
            double lon2 = GPSPos2[1];

            double dLon = lon2 - lon1;

            // Great-circle initial bearing
            double y = Math.Sin(dLon) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Sin(lat2) -
                       Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

            double bearing = Math.Atan2(y, x);   // range [-π, π]

            // Normalize to [0, 2π)
            if(bearing < 0)
                bearing += 2.0 * Math.PI;

            return bearing;
        }

        public static string GetBearingString(double bearing_rad) {
            // Convert to degrees
            double bearing_deg = bearing_rad * (180.0f / Math.PI);
            return FlightInstruments.RadToString((float)bearing_rad);
        }

        public static double Deg2Rad(double degrees) {
            return degrees * (Math.PI / 180.0f);
        }
        public static double GetDistance(double3 pos1, double3 pos2, double radius) {
            double dLat = pos2[0] - pos1[0];
            double dLon = pos2[1] - pos1[1];

            double sinLat = Math.Sin(dLat * 0.5);
            double sinLon = Math.Sin(dLon * 0.5);

            double a =
                sinLat * sinLat +
                Math.Cos(pos1[0]) * Math.Cos(pos2[0]) *
                sinLon * sinLon;

            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

            return radius * c;   // arc length
        }

        public static Airport GetNearestAirport(double3 GPSPos, List<Airport> airports) {
            double best_dist = double.MaxValue;
            Airport best_airport = null;
            foreach(Airport airport in airports) {
                if(airport.Runways.Count > 0) {
                    double airport_lat_rad = Deg2Rad(airport.Latitude_deg);
                    double airport_lon_rad = Deg2Rad(airport.longitude_deg);
                    double3 airportGPS = new double3(airport_lat_rad, airport_lon_rad, 0.0);
                    double distance = GetDistance(GPSPos, airportGPS, 1.0); // actual radius does not matter for ranking distances
                    if(distance < best_dist) {
                        best_dist = distance;
                        best_airport = airport;
                    }
                }
            }
            return best_airport;
        }


        public static double GetHeading(Vehicle vehicle) {

            return GetSurfaceAttitude(vehicle).Z;
        }
        public static double GetHeadingDeg(double heading) {
            double deg = (heading + Math.PI / 2.0) * (180.0 / Math.PI);
            deg %= 360.0;
            if(deg < 0) deg += 360.0;
            return deg;
        }
        public static string GetHeadingString(double heading, int decimals = 2) {
            string format = "F" + decimals;
            return $"{GetHeadingDeg(heading).ToString(format)}°";
        }
        public static double3 GetSurfaceAttitude(Vehicle vehicle) {
            double3 Body2Cci = vehicle.GetBody2Cci().ToRollYawPitchRadians();

            doubleQuat enuBody2Cci = VehicleReferenceFrameEx.GetEnuBody2Cci(vehicle.GetPositionCci()) ?? doubleQuat.Identity;
            doubleQuat attitudeQuat = doubleQuat.Concatenate(vehicle.GetBody2Cci(), enuBody2Cci.Inverse());
            double3 rpy = attitudeQuat.ToRollPitchYawRadians();

            return rpy;
        }
    }

    public class Airport {
        public string Ident { get; set; }
        public string Name { get; set; }
        public double longitude_deg { get; set; }
        public double Latitude_deg { get; set; }
        public List<Runway> Runways { get; set; }

    }

    public class Runway {
        // Loaded from JSON data
        public double Length_m { get; set; }
        public double Width_m { get; set; }
        public string Surface { get; set; }
        public string Le_Ident { get; set; }
        public double Le_Latitude_rad { get; set; }
        public double Le_Longitude_rad { get; set; }
        public double Le_Elevation_m { get; set; }
        public double Le_True_Heading_rad { get; set; }
        public string He_Ident { get; set; }
        public double He_Latitude_rad { get; set; }
        public double He_Longitude_rad { get; set; }
        public double He_Elevation_m { get; set; }
        public bool Use_LE { get; set; }
        public double glideSlope { get; set; }
        public double3 GetGPS() {
            if(Use_LE) {
                return new double3(Le_Latitude_rad, Le_Longitude_rad, Le_Elevation_m);
            } else {
                return new double3(He_Latitude_rad, He_Longitude_rad, He_Elevation_m);
            }
        }
        public string GetDescription() {
            return $"{Le_Ident}/{He_Ident} Length: {(int)Length_m} m Surface: {Surface}";
        }
        public string GetIdent() {
            return Use_LE ? Le_Ident : He_Ident;
        }
        // Get true heading of currently selected runway
        public double GetTrueHeading() {
            if(Use_LE) {
                return Le_True_Heading_rad;
            } else {
                return (Le_True_Heading_rad + Math.PI) % (2.0 * Math.PI);
            }
        }
        public double GetCurrentVerticalAngle(double3 GPSPos, double radius) {
            // Aircraft position
            double aircraftLat_rad = GPSPos.X;   // latitude
            double aircraftLon_rad = GPSPos.Y;   // longitude
            double aircraftAlt_m = GPSPos.Z;   // altitude

            // Runway end currently in use
            double3 rw = GetGPS();  // Returns (lat, lon, elev) in radians / meters

            double rwLat_rad = rw.X;
            double rwLon_rad = rw.Y;
            double rwAlt_m = rw.Z;

            double3 aircraftPos = new double3(aircraftLat_rad, aircraftLon_rad, aircraftAlt_m);
            double3 runwayPos = new double3(rwLat_rad, rwLon_rad, rwAlt_m);

            double horizontal_m = AvionicsMain.GetDistance(aircraftPos, runwayPos, radius);

            // Vertical difference: aircraft altitude above runway threshold
            double deltaAlt_m = aircraftAlt_m - rwAlt_m;

            // Vertical angle toward the runway end
            double angle_rad = Math.Atan2(deltaAlt_m, horizontal_m);

            return (float)angle_rad;
        }
        public double GetVerticalDeviation(double3 GPSPos, double radius) {
            return GetCurrentVerticalAngle(GPSPos, radius) - glideSlope;
        }

        public double GetLateralDeviation(double3 GPSPos, double heading) {
            double bearing = GetBearing(GPSPos);

            double diff = (GetTrueHeading() - bearing) % (2 * Math.PI);

            if(diff > Math.PI)
                diff -= 2 * Math.PI;
            else if(diff < -Math.PI)
                diff += 2 * Math.PI;

            return diff;
        }

        public double GetLateralDeviation_dist(double3 GPSPos, double radius) {
            // Runway (great-circle) definition
            double3 runway_gps = GetGPS();
            double runway_lat_rad = runway_gps[0];
            double runway_long_rad = runway_gps[1];
            double runway_heading_rad = GetTrueHeading();    // θ12, radians from north

            // Point (aircraft) position
            double point_lat_rad = GPSPos[0];
            double point_long_rad = GPSPos[1];

            // Angular distance between runway point (1) and aircraft point (3)
            // GetDistance returns arc length, so divide by radius to get central angle d13
            double d13 = AvionicsMain.GetDistance(runway_gps, GPSPos, radius) / radius;

            // Initial bearing from runway point (1) to aircraft point (3) -> θ13
            double theta13 = AvionicsMain.GetBearing(runway_gps, GPSPos);   // radians

            // Track of the great circle from the runway -> θ12
            double theta12 = runway_heading_rad;               // radians

            // Cross-track angular distance
            double xTrackAngle = Math.Asin(Math.Sin(d13) * Math.Sin(theta13 - theta12));

            // Convert angular distance to linear distance
            double lateralOffset = xTrackAngle * radius;

            return lateralOffset;
        }
        public double GetVerticalDeviation_dist(double3 GPSPos, double radius) {
            double vertical_angle = GetCurrentVerticalAngle(GPSPos, radius);
            double base_dist = Math.Cos(vertical_angle) * AvionicsMain.GetDistance(GPSPos, GetGPS(), radius);
            double correct_rise = Math.Tan(glideSlope) * base_dist;
            double current_rise = GPSPos.Z;
            return current_rise - correct_rise;
        }

        public double GetBearing(double3 GPSPos) {
            return AvionicsMain.GetBearing(GPSPos, GetGPS());
        }
    }
}
