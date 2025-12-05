using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using ModMenu;
using StarMap.API;
using System.Text.Json;

namespace Avionics {
    [StarMapMod]
    public class AvionicsModEntryPoint {
        [StarMapAllModsLoaded]
        public void fullyLoaded() {
            Patcher.Patch();
        }

        [StarMapUnload]
        public void unload() {
            Patcher.Unload();
        }

        List<Airport> airports;
        static ImInputString airport_text_buffer = new ImInputString(9);
        static float glide_slope_input = 3;

        // Pages (besides flight instruments)
        static bool runwaySelectionPageOn;
        static bool autopilotPageOn;

        // Autopilot
        internal AvionicsComputer vehicleAvionicsComputer;

        [ModMenuEntry("Avionics")]
        public static void DrawMenu() {

            if(ImGui.BeginMenu("Unit")) {
                if(ImGui.MenuItem("Kilometers")) {
                    UnitControler.ChangeUnit(UnitControler.UnitSystem.Kilometers);
                } else if(ImGui.MenuItem("Statute Miles")) {
                    UnitControler.ChangeUnit(UnitControler.UnitSystem.StatuteMiles);
                } else if(ImGui.MenuItem("Nautical Miles")) {
                    UnitControler.ChangeUnit(UnitControler.UnitSystem.NauticalMiles);
                }
                ImGui.EndMenu();
            }
            if(ImGui.MenuItem("Runway selection")) {
                AvionicsModEntryPoint.runwaySelectionPageOn = !AvionicsModEntryPoint.runwaySelectionPageOn;
            }
            if(ImGui.MenuItem("Autopilot")) {
                AvionicsModEntryPoint.autopilotPageOn = !AvionicsModEntryPoint.autopilotPageOn;
            }
            if(ImGui.MenuItem("HSI")) {
                FlightInstruments.hsiPageOn = !FlightInstruments.hsiPageOn;
            }
            if(ImGui.MenuItem("VSI")) {
                FlightInstruments.vsiPageOn = !FlightInstruments.vsiPageOn;
            }
            if(ImGui.MenuItem("Radar Altimeter")) {
                FlightInstruments.raPageOn = !FlightInstruments.raPageOn;
            }
            if(ImGui.MenuItem("ASI")) {
                FlightInstruments.asiPageOn = !FlightInstruments.asiPageOn;
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

            vehicleAvionicsComputer = new AvionicsComputer();
            UnitControler.OnChangeUnit += vehicleAvionicsComputer.autopilot.ChangeUnit;
        }

        [StarMapAfterGui]
        public void OnAfterUi(double dt) {
            if(vehicleAvionicsComputer.vehicle == null) {
                vehicleAvionicsComputer.vehicle = Program.ControlledVehicle;
            }

            string entered = airport_text_buffer.ToString().Trim().ToUpper();
            vehicleAvionicsComputer.SetTargetAirport(airports.Find(a => a.Ident.Equals(entered, StringComparison.OrdinalIgnoreCase)));


            vehicleAvionicsComputer.Update((float) dt);

            ImGuiWindowFlags flags = ImGuiWindowFlags.MenuBar;
            Vehicle controlledVehicle = Program.ControlledVehicle;
            if(controlledVehicle == null) {
                return;
            }
            FlightComputer flightComputer = controlledVehicle.FlightComputer;

            vehicleAvionicsComputer.pos_GPS = Geomath.GetGPSPosition(controlledVehicle);
            string positionText = Geomath.GetGPSPositionString(vehicleAvionicsComputer.pos_GPS);
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

            string headingText = Geomath.GetHeadingString(vehicleAvionicsComputer.heading);

            if(vehicleAvionicsComputer.fms.targetAirport != null) {
                double target_lat_rad = Geomath.Deg2Rad * vehicleAvionicsComputer.fms.targetAirport.Latitude_deg;
                double target_lon_rad = Geomath.Deg2Rad * vehicleAvionicsComputer.fms.targetAirport.longitude_deg;
                double3 targetGPS = new double3(target_lat_rad, target_lon_rad, 0);
                airportTargetGPSText = Geomath.GetGPSPositionString(targetGPS);

                if(vehicleAvionicsComputer.targetBearing_rad != null) {
                    bearingText = Geomath.GetBearingString(vehicleAvionicsComputer.targetBearing_rad.Value);
                }

                double distance = Geomath.GetDistance(vehicleAvionicsComputer.pos_GPS, targetGPS, controlledVehicle.Orbit.Parent.MeanRadius);
                airportDistanceText = UnitControler.DistanceToString((float)distance);
                if(vehicleAvionicsComputer.fms.targetAirport.Runways.Count > 0) {
                    chooseARunwayMenuText = "Choose a runway";
                } else {
                    chooseARunwayMenuText = "No runways at this areodome";
                }
            }
            if(vehicleAvionicsComputer.fms.targetRunway != null) {
                double3 runwayGPS = vehicleAvionicsComputer.fms.targetRunway.GetGPS();
                runwayBearing = Geomath.GetBearing(vehicleAvionicsComputer.pos_GPS, runwayGPS);
                runwayTargetGPSText = Geomath.GetGPSPositionString(runwayGPS);
                runwayDistance = Geomath.GetDistance(vehicleAvionicsComputer.pos_GPS, runwayGPS, controlledVehicle.Orbit.Parent.MeanRadius);
                runwayDistanceText = $"{runwayDistance:F2} m";
                runwayTargetText = vehicleAvionicsComputer.fms.targetRunway.GetIdent();
                double lateralDeviation = vehicleAvionicsComputer.fms.targetRunway.GetLateralDeviation(vehicleAvionicsComputer.pos_GPS, controlledVehicle.Orbit.Parent.MeanRadius);
                lateralDeviationText = UnitControler.RadToString((float)lateralDeviation);
                double verticalDeviation = vehicleAvionicsComputer.fms.targetRunway.GetVerticalDeviation(vehicleAvionicsComputer.pos_GPS, controlledVehicle.Orbit.Parent.MeanRadius);
                verticalDeviationText = UnitControler.RadToString((float)verticalDeviation);
                deviation = new double2(lateralDeviation, verticalDeviation);
                slope = vehicleAvionicsComputer.fms.targetRunway.GetCurrentVerticalAngle(vehicleAvionicsComputer.pos_GPS, controlledVehicle.Orbit.Parent.MeanRadius);
                angleText = UnitControler.RadToString((float)slope);
                swapRunwayDirectionButtonText = "Swap runway direction";

                vehicleAvionicsComputer.fms.targetRunway.glideSlopeRad = Geomath.Deg2Rad * glide_slope_input;
            }

            // Runway selection page
            if(AvionicsModEntryPoint.runwaySelectionPageOn) {
                ImGui.Begin("Runway selection", ref AvionicsModEntryPoint.runwaySelectionPageOn, flags);

                if(ImGui.BeginMenuBar()) {
                    if(ImGui.BeginMenu(chooseARunwayMenuText)) {
                        if(vehicleAvionicsComputer.fms.targetAirport != null && vehicleAvionicsComputer.fms.targetAirport.Runways.Count > 0) {
                            foreach(Runway runway in vehicleAvionicsComputer.fms.targetAirport.Runways) {
                                if(ImGui.MenuItem(runway.GetDescription())) {
                                    vehicleAvionicsComputer.fms.targetRunway = runway;
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
                    vehicleAvionicsComputer.fms.targetAirport = Geomath.GetNearestAirport(vehicleAvionicsComputer.pos_GPS, airports);
                    airport_text_buffer.SetValue(vehicleAvionicsComputer.fms.targetAirport.Ident);
                }
                if(ImGui.Button(swapRunwayDirectionButtonText)) {
                    if(vehicleAvionicsComputer.fms.targetRunway != null) {
                        if(vehicleAvionicsComputer.fms.targetRunway.Use_LE) {
                            vehicleAvionicsComputer.fms.targetRunway.Use_LE = false;
                        } else {
                            vehicleAvionicsComputer.fms.targetRunway.Use_LE = true;
                        }
                    }
                }
                ImGui.Text("Enter glideslope");
                ImGui.InputFloat("GS", ref glide_slope_input);
                ImGui.Text($"Vehicle position: {positionText}");
                ImGui.Text($"Vehicle heading: {headingText}");
                ImGui.Text("");
                ImGui.Text($"Selected Airport: {vehicleAvionicsComputer.fms.targetAirport?.Name ?? "None"}");
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


            // Autopilot page
            if(AvionicsModEntryPoint.autopilotPageOn) {
                ImGui.Begin("Autopilot", ref AvionicsModEntryPoint.autopilotPageOn, flags);
                if(ImGui.Button(vehicleAvionicsComputer.autopilot.engaged ? "Disengage Autopilot" : "Engage Autopilot")) {
                    if(vehicleAvionicsComputer.autopilot.engaged) {
                        vehicleAvionicsComputer.autopilot.Disengage();
                    } else {
                        vehicleAvionicsComputer.autopilot.Engage();
                    }
                }
                ImGui.Text($"Lateral Mode: {vehicleAvionicsComputer.autopilot.lateralMode}");
                if(ImGui.Button("HDG")) {
                    vehicleAvionicsComputer.autopilot.lateralMode = Autopilot.LateralMode.HeadingHold;
                }
                //ImGui.SameLine();
                //if(ImGui.Button("APR")) { }
                ImGui.SameLine();
                if(ImGui.Button("NAV")) {
                    vehicleAvionicsComputer.autopilot.lateralMode = Autopilot.LateralMode.Nav;
                }
                ImGui.Text($"Vertical Mode: {vehicleAvionicsComputer.autopilot.verticalMode}");
                if(ImGui.Button("ALT")) {
                    vehicleAvionicsComputer.autopilot.target_altitude_m = (int)vehicleAvionicsComputer.pos_GPS.Z;
                }
                //ImGui.SameLine();
                //if(ImGui.Button("VS")) { }
                //ImGui.SameLine();
                //if(ImGui.Button("VNav")) { }
                ImGui.Text("Alt. Select");
                if(ImGui.Button($"-{vehicleAvionicsComputer.autopilot.large_height_display_unit}")) {
                    vehicleAvionicsComputer.autopilot.target_altitude_display_value -= vehicleAvionicsComputer.autopilot.large_height_display_unit;
                }
                ImGui.SameLine();
                if(ImGui.Button($"-{vehicleAvionicsComputer.autopilot.small_height_display_unit}")) {
                    vehicleAvionicsComputer.autopilot.target_altitude_display_value -= vehicleAvionicsComputer.autopilot.small_height_display_unit;
                }
                ImGui.SameLine();
                if(ImGui.Button($"+{vehicleAvionicsComputer.autopilot.small_height_display_unit}")) {
                    vehicleAvionicsComputer.autopilot.target_altitude_display_value += vehicleAvionicsComputer.autopilot.small_height_display_unit;
                }
                ImGui.SameLine();
                if(ImGui.Button($"+{vehicleAvionicsComputer.autopilot.large_height_display_unit}")) {
                    vehicleAvionicsComputer.autopilot.target_altitude_display_value += vehicleAvionicsComputer.autopilot.large_height_display_unit;
                }
                string heightUnit = UnitControler.AltitudeToString(0f, 0, false, true).Replace("0 ", "");
                ImGui.InputFloat(heightUnit + "##xx", ref vehicleAvionicsComputer.autopilot.target_altitude_display_value);

                ImGui.Text("VS Select");
                if(ImGui.Button($"-{vehicleAvionicsComputer.autopilot.vs_display_unit}##xx")) {
                    vehicleAvionicsComputer.autopilot.target_vs_display_value -= vehicleAvionicsComputer.autopilot.vs_display_unit;
                }
                ImGui.SameLine();
                if(ImGui.Button($"+{vehicleAvionicsComputer.autopilot.vs_display_unit}##xx")) {
                    vehicleAvionicsComputer.autopilot.target_vs_display_value += vehicleAvionicsComputer.autopilot.vs_display_unit;
                }
                string speedUnit = UnitControler.SpeedToString(0f, 0).Replace("0 ", "");
                ImGui.InputFloat(speedUnit + "##xx", ref vehicleAvionicsComputer.autopilot.target_vs_display_value);

                // Update autopilot inputs
                vehicleAvionicsComputer.autopilot.current_vs = vehicleAvionicsComputer.verticalSpeed_mps;
                vehicleAvionicsComputer.autopilot.current_altitude = (float)vehicleAvionicsComputer.pos_GPS.Z;
                //Console.WriteLine(autopilot.GetDebugString());

                if(vehicleAvionicsComputer.fms.targetRunway != null) {
                    float bearing = (float)vehicleAvionicsComputer.fms.targetRunway.GetBearing(Geomath.GetGPSPosition(controlledVehicle)) - (float)Math.PI / 2f;
                    //autopilot.
                }
                ImGui.End();
            }
            
            // HSI, VSI, RA, ASI pages
            FlightInstruments.DrawFlightInstruments(vehicleAvionicsComputer);

            ImGui.Render();
        }
    }
}