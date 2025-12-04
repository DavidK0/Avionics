using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace Avionics {
    internal class RA {
        // Geometry
        static private float radius = 100f;
        static private float innerRadius = radius - 20f;
        static private float maxAngleRad = 135f * AvionicsMain.Deg2Rad; // ±135° sweep

        // Scale
        const float FeetPerMeter = 3.28084f;
        static float MaxAltMeter;

        static private float clampedAltM;

        // State
        static private float needleAltFeet;   // clamped value for the needle
        static private string altText = "0 FT";

        public RA() {
            // Constructor logic here
        }

        public static void Update(float radarAltitude_m) {
            // Digital readout – show the actual value, limited to non-negative
            altText = FlightInstruments.AltitudeToString(radarAltitude_m, 0);


            if(FlightInstruments.CurrentUnit == FlightInstruments.DistanceUnit.Kilometers) {
                MaxAltMeter = 20000f;
            } else {
                MaxAltMeter = 18288;
            }
            clampedAltM = MathF.Max(-MaxAltMeter, MathF.Min(MaxAltMeter, radarAltitude_m));
        }

        // Map altitude in meters to dial angle with a non-linear (log) mapping
        // 0 m   -> 45°
        // max   -> 315°
        // Low altitudes get more angular resolution than high altitudes.
        static private float AltToAngle(float meters) {
            // Normalize altitude into [0,1]
            float t = meters / MaxAltMeter;
            t = MathF.Max(0f, MathF.Min(1f, t));    // clamp 0..1

            // Non-linear shaping:
            // shape > 0: bigger shape => more emphasis on low altitudes
            const float shape = 10f;

            // Log curve normalized so that:
            //   t_raw = 0 -> 0
            //   t_raw = 1 -> 1
            float tNonLinear = MathF.Log(1f + shape * t) / MathF.Log(1f + shape);

            // Now map [0,1] -> angle sweep [π - maxAngleRad, π + maxAngleRad]
            // which corresponds to [45°, 315°] with maxAngleRad = 135°
            return (float)System.Math.PI - maxAngleRad + tNonLinear * (2f * maxAngleRad);
        }

        internal static unsafe void Render(ImDrawList* draw_list, float2 windowPos, float2 size) {
            ImColor8 white = new ImColor8(255, 255, 255, 255);
            ImColor8 green = new ImColor8(0, 255, 0, 255);

            // Center of the instrument
            float2 center = new float2(
                windowPos.X + size.X * 0.5f,
                windowPos.Y + size.Y * 0.5f
            );

            // Bezel
            ImDrawListExtensions.AddCircle(draw_list, center, radius, white, 0, 2f);

            // Tick marks and numeric labels
            float minorStep = MaxAltMeter / 10f;
            float majorStep = MaxAltMeter / 5f;

            for(float alt = 0f; alt <= MaxAltMeter; alt += minorStep) {
                float angle = AltToAngle(alt);
                float cosA = MathF.Cos(angle);
                float sinA = MathF.Sin(angle);

                bool major = Math.Abs(alt % majorStep) < 0.1f || Math.Abs(alt % majorStep) - majorStep < 0.1f;

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

                // Labels at major ticks (except 0)
                if(major && alt > 0f) {
                    float labelRadius = radius - 30f;
                    float2 labelPos = new float2(
                        center.X + cosA * labelRadius,
                        center.Y + sinA * labelRadius
                    );

                    string label = FlightInstruments.AltitudeToString(alt, 0, true);
                    ImDrawListExtensions.AddText(draw_list, labelPos - new float2(10f, 6f), white, label);
                }
            }

            // A prominent "0" at the top of the dial
            {
                float zeroAngle = AltToAngle(0f);
                float cosA = MathF.Cos(zeroAngle);
                float sinA = MathF.Sin(zeroAngle);
                float labelRadius = radius - 28f;
                float2 zeroPos = new float2(
                    center.X + cosA * labelRadius,
                    center.Y + sinA * labelRadius
                );
                ImDrawListExtensions.AddText(draw_list, zeroPos - new float2(6f, 6f), white, "0");
            }

            // Needle
            {
                float angle = AltToAngle(clampedAltM);
                float cosA = MathF.Cos(angle);
                float sinA = MathF.Sin(angle);

                float2 needleEnd = new float2(
                    center.X + cosA * innerRadius,
                    center.Y + sinA * innerRadius
                );
                float2 needleStart = new float2(
                    center.X - cosA * 8f,
                    center.Y - sinA * 8f
                );

                ImDrawListExtensions.AddLine(draw_list, needleStart, needleEnd, green, 3f);
                ImDrawListExtensions.AddCircleFilled(draw_list, center, 5f, white);
            }

            // Digital readout at the bottom
            {
                float2 textPos = new float2(
                    center.X - radius * 0.4f,
                    center.Y + radius * 0.15f
                );
                ImDrawListExtensions.AddText(draw_list, textPos, white, altText);
            }
        }
    }
}
