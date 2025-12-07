using Brutal.ImGuiApi;

namespace Avionics {
    internal static class AutopilotWindow {
        internal static bool pageOn = false;
        internal static void Toggle() {
            pageOn = !pageOn;
        }
        internal static void Render(AvionicsComputer avionicsComputer) {
            if(!pageOn) {
                return;
            }

            ImGui.Begin("Autopilot", ref pageOn);
            if(ImGui.Button(avionicsComputer.autopilot.engaged ? "Disengage Autopilot" : "Engage Autopilot")) {
                if(avionicsComputer.autopilot.engaged) {
                    avionicsComputer.autopilot.Disengage();
                } else {
                    avionicsComputer.autopilot.Engage();
                }
            }
            ImGui.Text($"Lateral Mode: {avionicsComputer.fd.lateralMode}");
            if(ImGui.Button("HDG")) {
                avionicsComputer.fd.lateralMode = FlightDirector.LateralMode.HeadingHold;
            }
            //ImGui.SameLine();
            //if(ImGui.Button("APR")) { }
            ImGui.SameLine();
            if(ImGui.Button("NAV")) {
                avionicsComputer.fd.lateralMode = FlightDirector.LateralMode.Nav;
            }
            ImGui.Text($"Vertical Mode: {avionicsComputer.fd.verticalMode}");
            if(ImGui.Button("ALT")) {
                avionicsComputer.fd.verticalMode = FlightDirector.VerticalMode.AltitudeHold;
            }
            ImGui.SameLine();
            if(ImGui.Button("VS")) {
                avionicsComputer.fd.verticalMode = FlightDirector.VerticalMode.VerticalSpeedHold;
            }
            ImGui.SameLine();
            if(ImGui.Button("VNav")) {
                avionicsComputer.fd.verticalMode = FlightDirector.VerticalMode.VNav;
            }
            ImGui.Text("Alt. Select");
            if(ImGui.Button($"-{UnitController.SmallDistanceToString(avionicsComputer.fd.large_height_m)}")) {
                avionicsComputer.fd.target_altitude_display_value.add_SI(-avionicsComputer.fd.large_height_m);
            }
            ImGui.SameLine();
            if(ImGui.Button($"-{UnitController.SmallDistanceToString(avionicsComputer.fd.small_height_m)}")) {
                avionicsComputer.fd.target_altitude_display_value.add_SI(-avionicsComputer.fd.small_height_m);
            }
            ImGui.SameLine();
            if(ImGui.Button($"+{UnitController.SmallDistanceToString(avionicsComputer.fd.small_height_m)}")) {
                avionicsComputer.fd.target_altitude_display_value.add_SI(avionicsComputer.fd.small_height_m);
            }
            ImGui.SameLine();
            if(ImGui.Button($"+{UnitController.SmallDistanceToString(avionicsComputer.fd.large_height_m)}")) {
                avionicsComputer.fd.target_altitude_display_value.add_SI(avionicsComputer.fd.large_height_m);
            }
            string heightUnit = UnitController.SmallDistanceToString(0f).Replace("0 ", "");
            ImGui.InputFloat(heightUnit + "##xx", ref avionicsComputer.fd.target_altitude_display_value.distance);

            ImGui.Text("VS Select");
            if(ImGui.Button($"-{UnitController.SmallSpeedToString(avionicsComputer.fd.vs_display_mps)}##xx")) {
                avionicsComputer.fd.target_vs_display_value.add_SI(-avionicsComputer.fd.vs_display_mps);
            }
            ImGui.SameLine();
            if(ImGui.Button($"+{UnitController.SmallSpeedToString(avionicsComputer.fd.vs_display_mps)}##xx")) {
                avionicsComputer.fd.target_vs_display_value.add_SI(avionicsComputer.fd.vs_display_mps);
            }
            string speedUnit = UnitController.SmallSpeedToString(0f, 0).Replace("0 ", "");
            ImGui.InputFloat(speedUnit + "##xx", ref avionicsComputer.fd.target_vs_display_value.speed);

            // Round target_altitude_display_value to nearest 100
            avionicsComputer.fd.target_altitude_display_value.distance = MathF.Round(avionicsComputer.fd.target_altitude_display_value.distance / 100f) * 100f;
            // Round target_vs_display_value
            if(UnitController.CurrentUnit == UnitController.UnitSystem.Kilometers) {
                avionicsComputer.fd.target_vs_display_value.speed = MathF.Round(avionicsComputer.fd.target_vs_display_value.speed / 1f) * 1f;
            } else {
                avionicsComputer.fd.target_vs_display_value.speed = MathF.Round(avionicsComputer.fd.target_vs_display_value.speed / 100f) * 100f;
            }

            // Update autopilot inputs
            avionicsComputer.fd.current_vs = avionicsComputer.verticalSpeed_mps;
            avionicsComputer.fd.current_altitude = (float)avionicsComputer.pos_GPS.Z;

            ImGui.End();
        }
    }
}
