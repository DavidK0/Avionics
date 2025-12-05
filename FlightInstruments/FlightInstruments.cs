using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace Avionics{
    internal class FlightInstruments {
        internal static bool hsiPageOn = false;
        internal static bool vsiPageOn = false;
        internal static bool raPageOn = false;
        internal static bool asiPageOn = false;

        private static ImGuiWindowFlags flags = ImGuiWindowFlags.MenuBar;

        internal static void DrawFlightInstruments(AvionicsComputer vehicleAvioniceComputer) {
            Draw_HSI(vehicleAvioniceComputer);
            Draw_VSI(vehicleAvioniceComputer);
            Draw_RA(vehicleAvioniceComputer);
            Draw_ASI(vehicleAvioniceComputer);
        }
        private static void Draw_HSI(AvionicsComputer vehicleAvioniceComputer) {
            // HSI page
            if(hsiPageOn) {
                ImGui.SetNextWindowSizeConstraints(new float2(300, 300), new float2(float.MaxValue, float.MaxValue));
                ImGui.Begin("HSI", ref hsiPageOn, flags);
                unsafe {
                    ImDrawList* draw_list = ImGui.GetWindowDrawList();
                    ImDrawList* draw_list1 = draw_list;
                    float2 center = ImGui.GetWindowPos();
                    float2 size = ImGui.GetWindowSize();

                    HorizontalSituationIndicator.Update(vehicleAvioniceComputer.heading, vehicleAvioniceComputer.fms.targetRunway, vehicleAvioniceComputer.targetDeviation, vehicleAvioniceComputer.targetBearing_rad, vehicleAvioniceComputer.targetDistance_m, vehicleAvioniceComputer.targetSlope_rad);
                    HorizontalSituationIndicator.Render(draw_list, center, size);
                }
                ImGui.End();
            }
        }
        private static void Draw_VSI(AvionicsComputer vehicleAvioniceComputer) {
            // VSI page
            if(vsiPageOn) {
                ImGui.SetNextWindowSizeConstraints(new float2(300, 300), new float2(float.MaxValue, float.MaxValue));
                ImGui.Begin("VSI", ref vsiPageOn, flags);
                unsafe {
                    ImDrawList* draw_list = ImGui.GetWindowDrawList();
                    ImDrawList* draw_list1 = draw_list;
                    float2 center = ImGui.GetWindowPos();
                    float2 size = ImGui.GetWindowSize();

                    VerticalSpeedIndicator.Update(vehicleAvioniceComputer.verticalSpeed_mps);
                    VerticalSpeedIndicator.Render(draw_list, center, size);
                }
                ImGui.End();
            }
        }
        private static void Draw_RA(AvionicsComputer vehicleAvioniceComputer) {
            // RA page
            if(raPageOn) {
                ImGui.SetNextWindowSizeConstraints(new float2(300, 300), new float2(float.MaxValue, float.MaxValue));
                ImGui.Begin("Radar Altimeter", ref raPageOn, flags);
                unsafe {
                    ImDrawList* draw_list = ImGui.GetWindowDrawList();
                    ImDrawList* draw_list1 = draw_list;
                    float2 center = ImGui.GetWindowPos();
                    float2 size = ImGui.GetWindowSize();

                    RadarAltimeter.Update((float)vehicleAvioniceComputer.radarAltitude);
                    RadarAltimeter.Render(draw_list, center, size);
                }
                ImGui.End();
            }
        }

        private static void Draw_ASI(AvionicsComputer vehicleAvioniceComputer) {
            // ASI page
            if(asiPageOn) {
                ImGui.SetNextWindowSizeConstraints(new float2(300, 300), new float2(float.MaxValue, float.MaxValue));
                ImGui.Begin("Airspeed Indicator", ref asiPageOn, flags);
                unsafe {
                    ImDrawList* draw_list = ImGui.GetWindowDrawList();
                    ImDrawList* draw_list1 = draw_list;
                    float2 center = ImGui.GetWindowPos();
                    float2 size = ImGui.GetWindowSize();

                    AirspeedIndicator.Update((float)vehicleAvioniceComputer.indicatedAirspeed_mps);
                    AirspeedIndicator.Render(draw_list, center, size);
                }
                ImGui.End();
            }
        }
    }
}
