using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace Avionics{
    public class FlightInstruments {
        public static bool hsiPageOn = false;
        public static bool vsiPageOn = false;
        public static bool raPageOn = false;
        public static bool asiPageOn = false;
        public static bool hiPageOn = false;
        public static bool tiPageOn = false;
        public static bool aiPageOn = false;

        public static ImGuiWindowFlags flags = (ImGuiWindowFlags)0;

        public static float2 minWindowSize = new float2(150, 150);

        public static void RenderFlightInstruments(AvionicsComputer vehicleAvioniceComputer) {
            Draw_HSI(vehicleAvioniceComputer);
            Draw_VSI(vehicleAvioniceComputer);
            Draw_RA(vehicleAvioniceComputer);
            Draw_ASI(vehicleAvioniceComputer);
            Draw_HI(vehicleAvioniceComputer);
            Draw_TI(vehicleAvioniceComputer);
            Draw_AI(vehicleAvioniceComputer);
        }
        public static void Draw_HSI(AvionicsComputer vehicleAvioniceComputer) {
            // HSI page
            if(hsiPageOn) {
                ImGui.SetNextWindowSizeConstraints(minWindowSize, new float2(float.MaxValue, float.MaxValue));
                ImGui.Begin("Horizontal Situation Indicator", ref hsiPageOn, flags);
                unsafe {
                    ImDrawList* draw_list = ImGui.GetWindowDrawList();
                    ImDrawList* draw_list1 = draw_list;
                    float2 center = ImGui.GetWindowPos();
                    float2 size = ImGui.GetWindowSize();

                    NavigationSystem.NavSolution sol = vehicleAvioniceComputer.navSystem.Current;

                    HorizontalSituationIndicator.Update(vehicleAvioniceComputer.heading, sol.DesiredTrack_rad, new float2(sol.CrossTrackError_rad, sol.VerticalPathError_rad), sol.BearingToTarget_rad, (float?)sol.DistanceToTarget_m, sol.DesiredPathSlope_rad);
                    HorizontalSituationIndicator.Render(draw_list, center, size);
                }
                ImGui.End();
            }
        }
        public static void Draw_VSI(AvionicsComputer vehicleAvioniceComputer) {
            // VSI page
            if(vsiPageOn) {
                ImGui.SetNextWindowSizeConstraints(minWindowSize, new float2(float.MaxValue, float.MaxValue));
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
        public static void Draw_RA(AvionicsComputer vehicleAvioniceComputer) {
            // RA page
            if(raPageOn) {
                ImGui.SetNextWindowSizeConstraints(minWindowSize, new float2(float.MaxValue, float.MaxValue));
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

        public static void Draw_ASI(AvionicsComputer vehicleAvioniceComputer) {
            // ASI page
            if(asiPageOn) {
                ImGui.SetNextWindowSizeConstraints(minWindowSize, new float2(float.MaxValue, float.MaxValue));
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
        public static void Draw_HI(AvionicsComputer vehicleAvioniceComputer) {
            // HI page
            if(hiPageOn) {
                ImGui.SetNextWindowSizeConstraints(minWindowSize, new float2(float.MaxValue, float.MaxValue));
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
        public static void Draw_TI(AvionicsComputer avioniceComputer) {
            // TI page
            if(tiPageOn) {
                ImGui.SetNextWindowSizeConstraints(minWindowSize, new float2(float.MaxValue, float.MaxValue));
                ImGui.Begin("Turn Indicator", ref tiPageOn, flags);
                unsafe {
                    ImDrawList* draw_list = ImGui.GetWindowDrawList();
                    float2 center = ImGui.GetWindowPos();
                    float2 size = ImGui.GetWindowSize();

                    TurnIndicator.Update(avioniceComputer.yawRate, avioniceComputer.slipAccelBodyY);
                    TurnIndicator.Render(draw_list, center, size);
                }
                ImGui.End();
            }
        }
        public static void Draw_AI(AvionicsComputer avioniceComputer) {
            // AI page
            if(aiPageOn) {
                ImGui.SetNextWindowSizeConstraints(minWindowSize, new float2(float.MaxValue, float.MaxValue));
                ImGui.Begin("Artificial Horizon", ref aiPageOn, flags);
                unsafe {
                    ImDrawList* draw_list = ImGui.GetWindowDrawList();
                    float2 center = ImGui.GetWindowPos();
                    float2 size = ImGui.GetWindowSize();

                    AttitudeIndicator.Update(avioniceComputer.pitch, avioniceComputer.roll);
                    AttitudeIndicator.Render(draw_list, center, size);
                }
                ImGui.End();
            }
        }
    }
}
