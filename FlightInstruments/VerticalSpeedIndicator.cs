using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace Avionics {
    internal class VerticalSpeedIndicator {
        static private float radius = 100f;
        static private float innerRadius = radius - 18f;
        static private float maxAngleRad = 135f * Geomath.Deg2Rad;
        static float maxVsMps;

        static private string vsText;
        static private float clampedVs;

        public VerticalSpeedIndicator() {
            // Constructor logic here
        }
        public static void Update(float verticalSpeed_mps) {
            vsText = UnitControler.SpeedToString(verticalSpeed_mps, 0);


            if(UnitControler.CurrentUnit == UnitControler.UnitSystem.Kilometers) {
                maxVsMps = 20f * 2f;
            } else {
                maxVsMps = 20.32f * 2f;
            }
            clampedVs = MathF.Max(-maxVsMps, MathF.Min(maxVsMps, verticalSpeed_mps));
        }
        internal static unsafe void Render(ImDrawList* draw_list, float2 windowPos, float2 size) {
            ImColor8 white = new ImColor8(255, 255, 255, 255);
            ImColor8 green = new ImColor8(0, 255, 0, 255);

            // Center and radius of the VSI
            float2 center = new float2(
                windowPos.X + size.X * 0.5f,
                windowPos.Y + size.Y * 0.5f
            );

            // Dial bezel
            ImDrawListExtensions.AddCircle(draw_list, center, radius, white, 0, 2f);

            // Helper: convert VS (fpm) to angle on the dial.
            // We use a 270° sweep: -max at bottom-left, +max at bottom-right, 0 at top.
            // Center angle at 180° (left), range ±135°.
            float VsToAngle(float vs) {
                float t = vs / maxVsMps;                  // -1..+1
                t = MathF.Max(-1f, MathF.Min(1f, t));
                return (float)Math.PI + t * maxAngleRad;
            }

            // Tick marks every 1/4 of max VS, major ticks every 1/2 max VS
            for(float vs = -maxVsMps; vs <= maxVsMps; vs += maxVsMps / 4f) {
                float angle = VsToAngle(vs);
                float cosA = MathF.Cos(angle);
                float sinA = MathF.Sin(angle);

                bool major = vs % maxVsMps / 2f == 0;
                float tickLen = major ? 14f : 8f;
                float tickThickness = major ? 2f : 1f;

                float2 pOuter = new float2(
                    center.X + cosA * radius,
                    center.Y + sinA * radius
                );
                float2 pInner = new float2(
                    center.X + cosA * (radius - tickLen),
                    center.Y + sinA * (radius - tickLen)
                );

                ImDrawListExtensions.AddLine(draw_list, pOuter, pInner, white, tickThickness);

                // Numeric labels for the major ticks (except 0)
                if(major && vs != 0) {
                    float labelRadius = radius - 30f;
                    float2 labelPos = new float2(
                        center.X + cosA * labelRadius,
                        center.Y + sinA * labelRadius
                    );

                    string label;
                    if(UnitControler.CurrentUnit == UnitControler.UnitSystem.Kilometers) {
                        // Show in m/s: 0.5, 1, 1.5, 2 etc.
                        float labelValue = MathF.Abs(vs);
                        label = labelValue.ToString("0.#");
                    } else {
                        // Show in thousands: 0.5, 1, 1.5, 2 etc.
                        float thousands = MathF.Abs(vs);
                        label = thousands.ToString("0.#");
                    }
                    ImDrawListExtensions.AddText(draw_list, labelPos - new float2(6f, 6f), white, label);
                }
            }

            // "UP" and "DOWN" labels on the right side of the dial
            {
                float upAngle = VsToAngle(+1500f);
                float downAngle = VsToAngle(-1500f);

                float labelR = radius - 45f;

                float2 upPos = new float2(
                    center.X + MathF.Cos(upAngle) * labelR,
                    center.Y + MathF.Sin(upAngle) * labelR
                );
                float2 downPos = new float2(
                    center.X + MathF.Cos(downAngle) * labelR,
                    center.Y + MathF.Sin(downAngle) * labelR
                );

                ImDrawListExtensions.AddText(draw_list, upPos, white, "UP");
                ImDrawListExtensions.AddText(draw_list, downPos, white, "DN");
            }

            // Needle
            {
                float needleAngle = VsToAngle(clampedVs);
                float cosA = MathF.Cos(needleAngle);
                float sinA = MathF.Sin(needleAngle);

                float2 needleEnd = new float2(
                    center.X + cosA * innerRadius,
                    center.Y + sinA * innerRadius
                );

                // Slightly inset start point so the center knob covers the joint
                float2 needleStart = new float2(
                    center.X - cosA * 8f,
                    center.Y - sinA * 8f
                );

                ImDrawListExtensions.AddLine(draw_list, needleStart, needleEnd, green, 3f);

                // Center knob
                ImDrawListExtensions.AddCircleFilled(draw_list, center, 5f, white);
            }

            // Digital readout at the bottom
            {
                float2 textPos = new float2(center.X - radius * 0.40f, center.Y + radius * 0.15f);
                ImDrawListExtensions.AddText(draw_list, textPos, white, vsText);
            }
        }
    }
}
