﻿using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using StarMap.API;
using System;
using System.Text.Json;

namespace AvionicsMod
{
    [StarMapMod]
    public class Avionics {
        List<Airport> airports;
        Airport selectedAirport;
        Runway selectedRunway;
        ImInputString buffer = new ImInputString(9);
        enum Page {
            Settings,
            HSI,
        }
        Page currentPage = Page.Settings;

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
            string entered = buffer.ToString().Trim().ToUpper();
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
            float runwayBearing = 0f;
            float runwayDistance = 0f;
            string runwayTargetText = "No runway selected";
            string lateralDeviationText = "No runway selected";
            double2 deviation = new double2(double.MaxValue, double.MaxValue);
            string angleText = "No runway selected";
            float slope = 0f;
            string swapRunwayDirectionButtonText = "No runway selected";
            string chooseARunwayMenuText = "No airport selected";


            float heading = GetHeading(controlledVehicle);
            string headingText = GetHeadingString(heading);

            if(selectedAirport != null) {
                float target_lat_rad = Deg2Rad((float)selectedAirport.Latitude_deg);
                float target_lon_rad = Deg2Rad((float)selectedAirport.longitude_deg);
                double3 targetGPS = new double3(target_lat_rad, target_lon_rad, 0);
                airportTargetGPSText = GetGPSPositionString(targetGPS);

                float bearing = GetBearing(positionGPS, targetGPS);
                bearingText = GetBearingString(bearing);

                double distance = GetDistance(positionGPS, targetGPS, controlledVehicle.Orbit.Parent.MeanRadius);
                airportDistanceText = $"{(int)distance/1000} km";
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
                runwayDistance = (float)GetDistance(positionGPS, runwayGPS, controlledVehicle.Orbit.Parent.MeanRadius);
                runwayDistanceText = $"{runwayDistance:F2} m";
                runwayTargetText = selectedRunway.GetIdent();
                double lateralDeviation = GetLateralDeviation(selectedRunway, positionGPS, controlledVehicle.Orbit.Parent.MeanRadius);
                lateralDeviationText = $"{lateralDeviation:F2} m";
                deviation = new double2(lateralDeviation, 0.0);
                slope = selectedRunway.GetCurrentVerticalAngle(positionGPS, (float)controlledVehicle.Orbit.Parent.MeanRadius);
                angleText = $"{slope * (180.0f / (float)Math.PI):F2}°";
                swapRunwayDirectionButtonText = "Swap runway direction";
            }

            // Main menu bar
            ImGui.Begin("Avionics", flags);
            if(ImGui.BeginMenuBar()) {
                if(ImGui.BeginMenu("Page")) {
                    foreach(Page page in Enum.GetValues(typeof(Page))) {
                        if(ImGui.MenuItem(page.ToString())) {
                            currentPage = page;
                        }
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMenuBar();
            }

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

            // Settings page
            if(currentPage == Page.Settings) {
                ImGui.Text("Enter airport ICAO");
                ImGui.InputText("", buffer);

                if(ImGui.Button("Nearest Aerodome with runway")) {
                    selectedAirport = GetNearestAirport(positionGPS, airports);
                    buffer.SetValue(selectedAirport.Ident);
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
                ImGui.Text($"Rising angle: {angleText}");
                ImGui.End();
            }

            // HSI page
            if(currentPage == Page.HSI) {
                unsafe {
                    ImDrawList* draw_list = ImGui.GetWindowDrawList();
                    ImDrawList* draw_list1 = draw_list;
                    float2 center = ImGui.GetWindowPos();
                    float2 size = ImGui.GetWindowSize();

                    Astronomical parent = controlledVehicle.Orbit.Parent;
                    Celestial celestial = (Celestial)controlledVehicle.Orbit.Parent;


                    RenderHSI(draw_list, center, size, heading, selectedRunway, deviation, runwayBearing, runwayDistance, slope);
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
        public static float GetBearing(double3 GPSPos1, double3 GPSPos2) {
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

            return (float)bearing;
        }

        public static string GetBearingString(float bearing_rad) {
            // Convert to degrees
            float bearing_deg = bearing_rad * (180.0f / (float)Math.PI);
            return $"{bearing_deg:F2}°";
        }

        public static float Deg2Rad(float degrees) {
            return degrees * ((float)Math.PI / 180.0f);
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
                    float airport_lat_rad = Deg2Rad((float)airport.Latitude_deg);
                    float airport_lon_rad = Deg2Rad((float)airport.longitude_deg);
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

        public static double GetLateralDeviation(Runway runway, double3 GPSPos, double radius) {
            // Runway (great-circle) definition
            double3 runway_gps = runway.GetGPS();
            double runway_lat_rad = runway_gps[0];
            double runway_long_rad = runway_gps[1];
            double runway_heading_rad = runway.GetTrueHeading();    // θ12, radians from north

            // Point (aircraft) position
            double point_lat_rad = GPSPos[0];
            double point_long_rad = GPSPos[1];

            // Angular distance between runway point (1) and aircraft point (3)
            // GetDistance returns arc length, so divide by radius to get central angle d13
            double d13 = GetDistance(runway_gps, GPSPos, radius) / radius;

            // Initial bearing from runway point (1) to aircraft point (3) -> θ13
            double theta13 = GetBearing(runway_gps, GPSPos);   // radians

            // Track of the great circle from the runway -> θ12
            double theta12 = runway_heading_rad;               // radians

            // Cross-track angular distance
            double xTrackAngle = Math.Asin(Math.Sin(d13) * Math.Sin(theta13 - theta12));

            // Convert angular distance to linear distance
            double lateralOffset = xTrackAngle * radius;

            return lateralOffset;
        }

        // Renders the Horizontal Situation Indicator (HSI)
        public static unsafe void RenderHSI(ImDrawList* draw_list, float2 windowPos, float2 size, float heading, Runway? _runway, double2 deviation, float bearing, float distance, float slope) {
            if (_runway == null) {
                ImDrawListExtensions.AddText(draw_list, new float2(windowPos.X, windowPos.Y + size.Y * 0.5f), new ImColor8(255, 255, 255, 255), "Select an airport and runway first");

                return;
            }
            Runway runway = _runway;


            // --- Deviation math ------------------------------------------------------
            float lateral_deviation_m = (float)deviation.X;

            float meters_per_dot = 10000f;
            float lateral_deviation_dots = lateral_deviation_m / meters_per_dot;

            // --- Basic CDI geometry --------------------------------------------------
            float cdi_radius = 100.0f;
            float cdi_thickness = 3f;
            float dot_radius = 5.0f;
            float pixels_per_dot = cdi_radius / 3.0f; // 1 dot = 1/3 of the radius

            // Center of the CDI on screen
            float2 cdi_center = new float2(
                windowPos.X + size.X * 0.5f,
                windowPos.Y + size.Y * 0.5f
            );
            int bearing_deg = (int)(bearing * (180.0f / (float)Math.PI));
            int heading_deg = (int)GetHeadingDeg(heading);
            int distance_int = (int)distance / 1000;
            int slope_deg = (int)(slope * (180.0f / (float)Math.PI));
            int slope_int = (int)slope_deg;
            string heading_text = $"{heading_deg}°";
            string bearing_text = $"{bearing_deg}°";
            string distance_text = $"{distance_int}km";
            string slope_text = $"{slope_int}°";
            
            ImDrawListExtensions.AddText(draw_list, cdi_center - new float2(cdi_radius, 20+cdi_radius), new ImColor8(255, 255, 255, 255), $"HDG:{heading_text}");
            ImDrawListExtensions.AddText(draw_list, cdi_center - new float2(cdi_radius, -cdi_radius), new ImColor8(255, 255, 255, 255), $"BRG:{bearing_text}");
            ImDrawListExtensions.AddText(draw_list, cdi_center - new float2(70-cdi_radius, 20+cdi_radius), new ImColor8(255, 255, 255, 255), $"DST:{distance_text}");
            ImDrawListExtensions.AddText(draw_list, cdi_center - new float2(70-cdi_radius, -cdi_radius), new ImColor8(255, 255, 255, 255), $"Slope:{slope_text}");


            // Transform from CDI-local (0,0 at cdi_center) to world, with rotation
            float2 LocalToWorld(float2 local, float angle = 0f) {
                float cosA = (float)Math.Cos(angle - heading + Math.PI/2);
                float sinA = (float)Math.Sin(angle - heading + Math.PI / 2);
                float xr = local.X * cosA - local.Y * sinA;
                float yr = local.X * sinA + local.Y * cosA; ;
                return new float2(cdi_center.X + xr, cdi_center.Y + yr);
            }

            ImColor8 white = new ImColor8(255, 255, 255, 255);
            ImColor8 green = new ImColor8(0, 255, 0, 255);

            // --- CDI circle (symmetric; rotation doesn't matter for center) ---------
            ImDrawListExtensions.AddCircle(draw_list, cdi_center, cdi_radius, white, 0, cdi_thickness);

            // Draw a small circle for the north and south markers
            float2 north_local = new float2(0f, cdi_radius);
            float2 south_local = new float2(0f, -cdi_radius);
            float2 west_local = new float2(-cdi_radius, 0f);
            float2 east_local = new float2(cdi_radius, 0f);
            ImDrawListExtensions.AddText(draw_list, LocalToWorld(north_local), white, "N");
            ImDrawListExtensions.AddText(draw_list, LocalToWorld(south_local), white, "S");
            ImDrawListExtensions.AddText(draw_list, LocalToWorld(west_local), white, "W");
            ImDrawListExtensions.AddText(draw_list, LocalToWorld(east_local), white, "E");

            // --- Deviation dots (defined in local CDI coordinates) -------------------
            float r1 = cdi_radius * 0.33f;
            float r2 = cdi_radius * 0.66f;

            // Lateral deviation dots (left/right)
            float2 left_dot1_local = new float2(-r1, 0.0f);
            float2 left_dot2_local = new float2(-r2, 0.0f);
            float2 right_dot1_local = new float2(r1, 0.0f);
            float2 right_dot2_local = new float2(r2, 0.0f);

            ImDrawListExtensions.AddCircle(draw_list, LocalToWorld(left_dot1_local, (float)runway.GetTrueHeading()), dot_radius, white, 0);
            ImDrawListExtensions.AddCircle(draw_list, LocalToWorld(left_dot2_local, (float)runway.GetTrueHeading()), dot_radius, white, 0);
            ImDrawListExtensions.AddCircle(draw_list, LocalToWorld(right_dot1_local, (float)runway.GetTrueHeading()), dot_radius, white, 0);
            ImDrawListExtensions.AddCircle(draw_list, LocalToWorld(right_dot2_local, (float)runway.GetTrueHeading()), dot_radius, white, 0);

            // --- Deviation needles (also defined in local CDI coordinates) ----------
            float lateral_deviation_pixels = lateral_deviation_dots * pixels_per_dot;

            // Lateral deviation needle (nominally vertical line in local space)
            for(int i = 0; i < 3; i++) { 
                float2 lateral_needle_p1_local = new float2(lateral_deviation_pixels / (float)Math.Pow(10, i), -cdi_radius * .5f);
                float2 lateral_needle_p2_local = new float2(lateral_deviation_pixels / (float)Math.Pow(10, i), cdi_radius * .5f);
                float2 lateral_needle_p1 = LocalToWorld(lateral_needle_p1_local, (float)runway.GetTrueHeading());
                float2 lateral_needle_p2 = LocalToWorld(lateral_needle_p2_local, (float)runway.GetTrueHeading());
                ImDrawListExtensions.AddLine(draw_list, lateral_needle_p1, lateral_needle_p2, green, i*2);
            }

            // Runway alignment needle
            float2 alignment_needle_p1_local = new float2(0, -cdi_radius);
            float2 alignment_needle_p2_local = new float2(0, -cdi_radius * .55f);
            float2 alignment_needle_p3_local = new float2(0, cdi_radius * .55f);
            float2 alignment_needle_p4_local = new float2(0, cdi_radius);
            float2 alignment_needle_1_p1 = LocalToWorld(alignment_needle_p1_local, (float)runway.GetTrueHeading());
            float2 alignment_needle_1_p2 = LocalToWorld(alignment_needle_p2_local, (float)runway.GetTrueHeading());
            float2 alignment_needle_2_p1 = LocalToWorld(alignment_needle_p3_local, (float)runway.GetTrueHeading());
            float2 alignment_needle_2_p2 = LocalToWorld(new float2(0, cdi_radius*.9f), (float)runway.GetTrueHeading());
            ImDrawListExtensions.AddLine(draw_list, alignment_needle_1_p1, alignment_needle_1_p2, green, 0);
            ImDrawListExtensions.AddLine(draw_list, alignment_needle_2_p1, alignment_needle_2_p2, green, 0);
            ImDrawListExtensions.AddCircle(draw_list, alignment_needle_2_p2, dot_radius, green, 0);

        }
        public static float GetHeading(Vehicle vehicle) {

            return (float)GetSurfaceAttitude(vehicle).Z;
        }
        public static double GetHeadingDeg(float heading) {
            double deg = (heading + Math.PI / 2.0) * (180.0 / Math.PI);
            deg %= 360.0;
            if(deg < 0) deg += 360.0;
            return deg;
        }
        public static string GetHeadingString(float heading, int decimals = 2) {
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
        public float Length_m { get; set; }
        public float Width_m { get; set; }
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
        public float GetCurrentVerticalAngle(double3 GPSPos, float radius) {
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

            double horizontal_m = Avionics.GetDistance(aircraftPos, runwayPos, radius);

            // Vertical difference: aircraft altitude above runway threshold
            double deltaAlt_m = aircraftAlt_m - rwAlt_m;

            // Vertical angle toward the runway end
            double angle_rad = Math.Atan2(deltaAlt_m, horizontal_m);

            return (float)angle_rad;
        }

    }
}
