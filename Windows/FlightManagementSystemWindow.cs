using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace Avionics.Windows {
    internal static class FlightManagementSystemWindow {
        internal static bool pageOn = false;
        internal static void Toggle() {
            pageOn = !pageOn;
        }

        public static ImInputString airport_text_buffer = new ImInputString(9);
        public static float glide_slope_input = 3;
        public static Airport selectedAirport;

        public static void Render(AvionicsComputer avionicsComputer) {
            if(pageOn) {

                string entered = airport_text_buffer.ToString().Trim().ToUpper();
                selectedAirport = FlightManagementSystem.airports.Find(a => a.Ident.Equals(entered, StringComparison.OrdinalIgnoreCase));

                ImGuiWindowFlags flags = ImGuiWindowFlags.MenuBar;
                Vehicle controlledVehicle = Program.ControlledVehicle;
                if(controlledVehicle == null) {
                    return;
                }
                FlightComputer flightComputer = controlledVehicle.FlightComputer;

                avionicsComputer.pos_GPS = Geomath.GetGPSPosition(controlledVehicle);
                string positionText = Geomath.GetGPSPositionString(avionicsComputer.pos_GPS);
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

                string headingText = Geomath.GetHeadingString(avionicsComputer.heading);

                if(selectedAirport != null) {
                    double target_lat_rad = Geomath.Deg2Rad * selectedAirport.Latitude_deg;
                    double target_lon_rad = Geomath.Deg2Rad * selectedAirport.longitude_deg;
                    double3 targetGPS = new double3(target_lat_rad, target_lon_rad, 0);
                    airportTargetGPSText = Geomath.GetGPSPositionString(targetGPS);

                    if(avionicsComputer.navSystem.targetBearing_rad != null) {
                        bearingText = Geomath.GetBearingString(avionicsComputer.navSystem.targetBearing_rad);
                    }

                    double distance = Geomath.GetDistance(avionicsComputer.pos_GPS, targetGPS, controlledVehicle.Orbit.Parent.MeanRadius);
                    airportDistanceText = UnitController.BigDistanceToString((float)distance);
                    if(selectedAirport.Runways.Count > 0) {
                        chooseARunwayMenuText = "Choose a runway";
                    } else {
                        chooseARunwayMenuText = "No runways at this areodome";
                    }
                }
                if(selectedAirport != null) {
                    double3 runwayGPS = selectedAirport.Runways[0].GetGPS();
                    runwayBearing = Geomath.GetBearing(avionicsComputer.pos_GPS, runwayGPS);
                    runwayTargetGPSText = Geomath.GetGPSPositionString(runwayGPS);
                    runwayDistance = Geomath.GetDistance(avionicsComputer.pos_GPS, runwayGPS, controlledVehicle.Orbit.Parent.MeanRadius);
                    runwayDistanceText = $"{runwayDistance:F2} m";
                    runwayTargetText = selectedAirport.Runways[0].GetIdent();
                    double lateralDeviation = selectedAirport.Runways[0].GetLateralDeviation(avionicsComputer.pos_GPS, controlledVehicle.Orbit.Parent.MeanRadius);
                    lateralDeviationText = UnitController.RadToString((float)lateralDeviation);
                    double verticalDeviation = selectedAirport.Runways[0].GetVerticalDeviation(avionicsComputer.pos_GPS, controlledVehicle.Orbit.Parent.MeanRadius);
                    verticalDeviationText = UnitController.RadToString((float)verticalDeviation);
                    deviation = new double2(lateralDeviation, verticalDeviation);
                    slope = selectedAirport.Runways[0].GetCurrentVerticalAngle(avionicsComputer.pos_GPS, controlledVehicle.Orbit.Parent.MeanRadius);
                    angleText = UnitController.RadToString((float)slope);
                    swapRunwayDirectionButtonText = "Swap runway direction";

                    selectedAirport.Runways[0].glideSlopeRad = Geomath.Deg2Rad * glide_slope_input;
                }
                ImGui.Begin("Flight Management System", ref pageOn, flags);

                if(ImGui.BeginMenuBar()) {
                    if(ImGui.BeginMenu(chooseARunwayMenuText)) {
                        if(selectedAirport != null && selectedAirport.Runways.Count > 0) {
                            foreach(Runway runway in selectedAirport.Runways) {
                                if(ImGui.MenuItem(runway.GetDescription())) {
                                    avionicsComputer.SetApproach(runway);
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
                    selectedAirport = Geomath.GetNearestAirport(avionicsComputer.pos_GPS, FlightManagementSystem.airports);
                    airport_text_buffer.SetValue(selectedAirport.Ident);
                }
                if(ImGui.Button(swapRunwayDirectionButtonText)) {
                    if(avionicsComputer.fms.ActiveApproach != null) {
                        if(avionicsComputer.fms.ActiveApproach.Runway.Use_LE) {
                            avionicsComputer.fms.ActiveApproach.Runway.Use_LE = false;
                        } else {
                            avionicsComputer.fms.ActiveApproach.Runway.Use_LE = true;
                        }
                    }
                }
                ImGui.Text("Enter glideslope");
                ImGui.InputFloat("GS", ref glide_slope_input);
                ImGui.Text($"Vehicle position: {positionText}");
                ImGui.Text($"Vehicle heading: {headingText}");
                ImGui.Text("");
                ImGui.Text($"Selected Airport: {selectedAirport?.Name ?? "None"}");
                ImGui.Text($"Airport position: {airportTargetGPSText}");
                ImGui.Text($"Bearing: {bearingText}");
                ImGui.Text($"Distance: {airportDistanceText}");
                ImGui.Text("");
                ImGui.Text($"Selected Runway: {runwayTargetText}");
                ImGui.Text($"Lateral deviation: {lateralDeviationText}");
                ImGui.Text($"Vertical deviation: {verticalDeviationText}");
                ImGui.Text($"Rising angle: {angleText}");
                ImGui.End();
            }
        }
    }
}
