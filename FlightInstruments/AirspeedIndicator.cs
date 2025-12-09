using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace Avionics {
    public class AirspeedIndicator {
        // Geometry
        public static float maxAngleRad = 150f * Geomath.Deg2Rad; // ±150° sweep (300° total)

        // Scale
        public static float maxSpeedMps;  // Maximum speed in m/s

        // State
        public static float clampedSpeedMps;
        public static string speedText = "0";

        public AirspeedIndicator() {
            // Constructor logic here
        }

        public static void Update(float airspeed_mps) {
            // Digital readout – show the actual value, limited to non-negative
            speedText = AirspeedToString(airspeed_mps, 0);

            // Set max speed based on unit system
            // ~400 knots (205 m/s) or ~740 km/h for imperial/metric
            if (UnitController.CurrentUnit == UnitController.UnitSystem.Kilometers) {
                maxSpeedMps = 205f; // ~740 km/h
            } else {
                maxSpeedMps = 205f; // ~400 knots
            }

            clampedSpeedMps = MathF.Max(0f, MathF.Min(maxSpeedMps, airspeed_mps));
        }

        // Convert airspeed to display string with appropriate units
        public static string AirspeedToString(float speedMps, int digits = 0, bool removeSuffix = false) {
            float value;
            string suffix;

            if (UnitController.CurrentUnit == UnitController.UnitSystem.Kilometers) {
                // Convert m/s to km/h
                value = speedMps * 3.6f;
                suffix = "km/h";
            } else {
                // Convert m/s to knots (1 knot = 0.514444 m/s)
                value = speedMps / 0.514444f;
                suffix = "kts";
            }

            if (removeSuffix) {
                return $"{value.ToString($"F{digits}")}";
            } else {
                return $"{value.ToString($"F{digits}")} {suffix}";
            }
        }

        // Map airspeed in m/s to dial angle
        // 0 m/s    -> bottom-left (30° from bottom, or 210° from right)
        // max m/s  -> bottom-right (330° from right)
        // Sweep: 300° total, clockwise from min to max
        public static float SpeedToAngle(float speedMps) {
            // Normalize speed into [0,1]
            float t = speedMps / maxSpeedMps;
            t = MathF.Max(0f, MathF.Min(1f, t)); // clamp 0..1

            // Map [0,1] -> angle sweep
            // Start at π + maxAngleRad (bottom-left), sweep clockwise to π - maxAngleRad (bottom-right)
            // This gives a natural clockwise increase with speed
            float startAngle = (float)Math.PI + maxAngleRad;  // ~330° or 5.76 rad
            float endAngle = (float)Math.PI - maxAngleRad;    // ~30° or 0.52 rad

            // Clockwise means decreasing angle
            return startAngle - t * (2f * maxAngleRad);
        }

        internal static unsafe void Render(ImDrawList* draw_list, float2 windowPos, float2 size) {
            // Center and size
            float radius = Math.Min(size.X, size.Y) * 0.45f;
            float innerRadius = radius * .82f;
            float2 center = new float2(
                windowPos.X + size.X * 0.5f,
                windowPos.Y + size.Y - radius
            );

            ImColor8 white = new ImColor8(255, 255, 255, 255);
            ImColor8 green = new ImColor8(0, 255, 0, 255);

            // Bezel
            ImDrawListExtensions.AddCircle(draw_list, center, radius, white, 0, 2f);

            // Get display values for tick marks
            float maxDisplayValue;
            float majorStep;
            float minorStep;

            if (UnitController.CurrentUnit == UnitController.UnitSystem.Kilometers) {
                maxDisplayValue = maxSpeedMps * 3.6f; // km/h
                majorStep = 100f;  // Major tick every 100 km/h
                minorStep = 20f;   // Minor tick every 20 km/h
            } else {
                maxDisplayValue = maxSpeedMps / 0.514444f; // knots
                majorStep = 50f;   // Major tick every 50 knots
                minorStep = 10f;   // Minor tick every 10 knots
            }

            // Tick marks and numeric labels
            for (float displaySpeed = 0f; displaySpeed <= maxDisplayValue; displaySpeed += minorStep) {
                // Convert display speed back to m/s for angle calculation
                float speedMps;
                if (UnitController.CurrentUnit == UnitController.UnitSystem.Kilometers) {
                    speedMps = displaySpeed / 3.6f;
                } else {
                    speedMps = displaySpeed * 0.514444f;
                }

                float angle = SpeedToAngle(speedMps);
                float cosA = MathF.Cos(angle);
                float sinA = MathF.Sin(angle);

                bool major = Math.Abs(displaySpeed % majorStep) < 0.1f;

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

                // Labels at major ticks
                if (major) {
                    float labelRadius = radius - 28f;
                    float2 labelPos = new float2(
                        center.X + cosA * labelRadius,
                        center.Y + sinA * labelRadius
                    );

                    string label = displaySpeed.ToString("0");
                    // Offset for text centering
                    float xOffset = label.Length * 3f;
                    ImDrawListExtensions.AddText(draw_list, labelPos - new float2(xOffset, 6f), white, label);
                }
            }

            // Needle
            {
                float angle = SpeedToAngle(clampedSpeedMps);
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
                    center.X - radius * 0.45f,
                    center.Y + radius * 0.15f
                );
                ImDrawListExtensions.AddText(draw_list, textPos, white, speedText);
            }
        }
    }
}
