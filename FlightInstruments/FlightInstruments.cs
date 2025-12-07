using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace Avionics{
    internal class FlightInstruments {
        internal static bool hsiPageOn = false;
        internal static bool vsiPageOn = false;
        internal static bool raPageOn = false;
        internal static bool asiPageOn = false;
        internal static bool hiPageOn = false;
        internal static bool tiPageOn = false;

        private static ImGuiWindowFlags flags = (ImGuiWindowFlags)0;

        internal static void RenderFlightInstruments(AvionicsComputer vehicleAvioniceComputer) {
            Draw_HSI(vehicleAvioniceComputer);
            Draw_VSI(vehicleAvioniceComputer);
            Draw_RA(vehicleAvioniceComputer);
            Draw_ASI(vehicleAvioniceComputer);
            Draw_HI(vehicleAvioniceComputer);
            Draw_TI(vehicleAvioniceComputer);
        }
        private static void Draw_HSI(AvionicsComputer vehicleAvioniceComputer) {
            // HSI page
            if(hsiPageOn) {
                ImGui.SetNextWindowSizeConstraints(new float2(300, 300), new float2(float.MaxValue, float.MaxValue));
                ImGui.Begin("Horizontal Situation Indicator", ref hsiPageOn, flags);
                unsafe {
                    ImDrawList* draw_list = ImGui.GetWindowDrawList();
                    ImDrawList* draw_list1 = draw_list;
                    float2 center = ImGui.GetWindowPos();
                    float2 size = ImGui.GetWindowSize();

                    HorizontalSituationIndicator.Update(vehicleAvioniceComputer.heading, vehicleAvioniceComputer.fms.ActiveApproach, vehicleAvioniceComputer.navSystem.targetDeviation, vehicleAvioniceComputer.navSystem.targetBearing_rad, (float)vehicleAvioniceComputer.navSystem.targetDistance_m, vehicleAvioniceComputer.navSystem.targetSlope_rad);
                    HorizontalSituationIndicator.Render(draw_list, center, size);
                }
                ImGui.End();
            }
        }
        private static void Draw_VSI(AvionicsComputer vehicleAvioniceComputer) {
            // VSI page
            if(vsiPageOn) {
                ImGui.SetNextWindowSizeConstraints(new float2(300, 300), new float2(float.MaxValue, float.MaxValue));
                ImGui.Begin("Vertical Speed Indicator", ref vsiPageOn, flags);
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
        private static void Draw_HI(AvionicsComputer vehicleAvioniceComputer) {
            // HI page
            if(hiPageOn) {
                ImGui.SetNextWindowSizeConstraints(new float2(300, 300), new float2(float.MaxValue, float.MaxValue));
                ImGui.Begin("Heading Indicator", ref hiPageOn, flags);
                unsafe {
                    ImDrawList* draw_list = ImGui.GetWindowDrawList();
                    float2 center = ImGui.GetWindowPos();
                    float2 size = ImGui.GetWindowSize();

                    HeadingIndicator.Update(vehicleAvioniceComputer.heading);
                    HeadingIndicator.Render(draw_list, center, size);
                }
                ImGui.End();
            }
        }
        private static void Draw_TI(AvionicsComputer avioniceComputer) {
            // TI page
            if(tiPageOn) {
                ImGui.SetNextWindowSizeConstraints(new float2(300, 300), new float2(float.MaxValue, float.MaxValue));
                ImGui.Begin("Turn Indicator", ref tiPageOn, flags);
                unsafe {
                    ImDrawList* draw_list = ImGui.GetWindowDrawList();
                    float2 center = ImGui.GetWindowPos();
                    float2 size = ImGui.GetWindowSize();

                    TurnIndicator.Update(avioniceComputer.yawRate, avioniceComputer.lateral_acceleration);
                    TurnIndicator.Render(draw_list, center, size);
                }
                ImGui.End();
            }
        }
    }
}
