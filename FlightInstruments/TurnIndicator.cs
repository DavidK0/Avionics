using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace Avionics {
    internal class TurnIndicator {
        // Geometry
        static private float radius = 100f;
        static private float innerRadius = radius - 24f;

        // Turn scale parameters
        static private float standardRateDegPerSec = 3f;                       // "Rate 1" = 3°/s
        static private float standardRateRadPerSec = standardRateDegPerSec * Geomath.Deg2Rad;
        static private float maxRateRadPerSec = 2f * standardRateRadPerSec;    // full deflection ~6°/s

        static private float maxDeflectionDeg = 25f;                           // visual needle deflection
        static private float maxDeflectionRad = maxDeflectionDeg * Geomath.Deg2Rad;

        // Slip ball parameters
        static private float g = 9.80665f;
        static private float ballFullScaleAccel = 0.25f * g;                   // ~0.25 g sideways for full-scale
        static private float ballTrackHalfWidth = 35f;
        static private float ballRadius = 6f;

        // Smoothing
        static private float smoothing = 0.2f;                                  // 0..1, higher = less smoothing

        // State
        static private float filteredYawRateRadPerSec = 0f;
        static private float filteredSlipAccel = 0f;
        static private string rateText = "LVL";

        public TurnIndicator() {
            // Constructor logic here (if needed)
        }

        public static void Update(float yawRate_radps, float lateralAccel_mps2) {
            // Clamp yaw rate to visual scale range
            float clampedYaw = MathF.Max(-maxRateRadPerSec, MathF.Min(maxRateRadPerSec, yawRate_radps));

            // Clamp lateral acceleration for ball travel
            float clampedSlip = MathF.Max(-ballFullScaleAccel, MathF.Min(ballFullScaleAccel, lateralAccel_mps2));

            // Simple exponential smoothing to avoid jitter
            filteredYawRateRadPerSec += (clampedYaw - filteredYawRateRadPerSec) * smoothing;
            filteredSlipAccel += (clampedSlip - filteredSlipAccel) * smoothing;

            // Textual description of the turn rate
            float yawRateDeg = filteredYawRateRadPerSec / Geomath.Deg2Rad;
            float absDeg = MathF.Abs(yawRateDeg);

            if(absDeg < 0.5f) {
                rateText = "0\ndeg/sec";
            } else {
                string dir = yawRateDeg > 0f ? "R" : "L";

                rateText = $"{dir} {Math.Abs(Math.Round(yawRateDeg * 10f) / 10f)}\ndeg/sec";
            }
        }

        /// <summary>
        /// Map yaw rate (rad/s) to a needle angle in radians.
        /// Zero rate -> straight up, positive rate -> deflect needle to the right.
        /// </summary>
        static private float YawRateToAngle(float yawRate_radps) {
            float t = yawRate_radps / maxRateRadPerSec; // -1..+1
            if(t < -1f) t = -1f;
            if(t > 1f) t = 1f;

            const float zeroAngleRad = -MathF.PI / 2f; // straight up in screen coordinates
            return zeroAngleRad + t * maxDeflectionRad;
        }

        /// <summary>
        /// Map lateral acceleration (m/s²) to horizontal slip ball offset in pixels.
        /// Positive acceleration -> ball to the right (assuming rightward accel is positive).
        /// </summary>
        static private float SlipToOffset(float lateralAccel_mps2) {
            float t = lateralAccel_mps2 / ballFullScaleAccel; // -1..+1
            if(t < -1f) t = -1f;
            if(t > 1f) t = 1f;

            return t * ballTrackHalfWidth;
        }

        internal static unsafe void Render(ImDrawList* draw_list, float2 windowPos, float2 size) {
            ImColor8 white = new ImColor8(255, 255, 255, 255);
            ImColor8 yellow = new ImColor8(255, 255, 0, 255);
            ImColor8 orange = new ImColor8(255, 165, 0, 255);

            // Center of the instrument
            float2 center = new float2(
                windowPos.X + size.X * 0.5f,
                windowPos.Y + size.Y * 0.5f
            );

            // Bezel
            ImDrawListExtensions.AddCircle(draw_list, center, radius, white, 0, 2f);

            // Turn scale arc (L / R marks)
            {
                const float zeroAngleRad = -MathF.PI / 2f; // up
                float scaleRadius = radius - 26f;
                float startAngle = zeroAngleRad - maxDeflectionRad * 1.2f;
                float endAngle = zeroAngleRad + maxDeflectionRad * 1.2f;
                int segments = 24;

                float prevAngle = startAngle;
                float2 prev = new float2(
                    center.X + MathF.Cos(prevAngle) * scaleRadius,
                    center.Y + MathF.Sin(prevAngle) * scaleRadius
                );

                for(int i = 1; i <= segments; ++i) {
                    float t = (float)i / segments;
                    float a = startAngle + (endAngle - startAngle) * t;
                    float2 p = new float2(
                        center.X + MathF.Cos(a) * scaleRadius,
                        center.Y + MathF.Sin(a) * scaleRadius
                    );

                    ImDrawListExtensions.AddLine(draw_list, prev, p, white, 2f);
                    prev = p;
                }

                // Center tick (wings-level mark)
                {
                    float angle = zeroAngleRad;
                    float2 outer = new float2(
                        center.X + MathF.Cos(angle) * (scaleRadius + 4f),
                        center.Y + MathF.Sin(angle) * (scaleRadius + 4f)
                    );
                    float2 inner = new float2(
                        center.X + MathF.Cos(angle) * (scaleRadius - 8f),
                        center.Y + MathF.Sin(angle) * (scaleRadius - 8f)
                    );
                    ImDrawListExtensions.AddLine(draw_list, outer, inner, white, 2f);
                }

                // Standard-rate ticks (left / right)
                for(int s = -1; s <= 1; s += 2) {
                    float yaw = standardRateRadPerSec * s;
                    float angle = YawRateToAngle(yaw);

                    float2 outer = new float2(
                        center.X + MathF.Cos(angle) * (scaleRadius + 2f),
                        center.Y + MathF.Sin(angle) * (scaleRadius + 2f)
                    );
                    float2 inner = new float2(
                        center.X + MathF.Cos(angle) * (scaleRadius - 10f),
                        center.Y + MathF.Sin(angle) * (scaleRadius - 10f)
                    );
                    ImDrawListExtensions.AddLine(draw_list, outer, inner, white, 2.5f);

                    // "L" and "R" labels near the standard-rate marks
                    float labelRadius = scaleRadius - 20f;
                    float2 labelPos = new float2(
                        center.X + MathF.Cos(angle) * labelRadius,
                        center.Y + MathF.Sin(angle) * labelRadius
                    );
                    string label = s > 0 ? "R" : "L";
                    ImDrawListExtensions.AddText(draw_list, labelPos - new float2(4f, 6f), yellow, label);
                }
            }

            // Turn needle (classic needle-style indication)
            {
                float angle = YawRateToAngle(filteredYawRateRadPerSec);
                float cosA = MathF.Cos(angle);
                float sinA = MathF.Sin(angle);

                float needleLength = innerRadius;
                float2 tip = new float2(
                    center.X + cosA * needleLength,
                    center.Y + sinA * needleLength
                );
                float2 basePos = new float2(
                    center.X - cosA * 10f,
                    center.Y - sinA * 10f
                );

                ImDrawListExtensions.AddLine(draw_list, basePos, tip, orange, 3f);
                ImDrawListExtensions.AddCircleFilled(draw_list, center, 4f, white);
            }

            // Fixed aircraft symbol in center (mini airplane)
            {
                float symbolSize = 12f;

                // Fuselage
                ImDrawListExtensions.AddLine(
                    draw_list,
                    new float2(center.X, center.Y - symbolSize),
                    new float2(center.X, center.Y + symbolSize * 0.6f),
                    orange,
                    2f
                );

                // Wings
                ImDrawListExtensions.AddLine(
                    draw_list,
                    new float2(center.X - symbolSize * 1.4f, center.Y),
                    new float2(center.X + symbolSize * 1.4f, center.Y),
                    orange,
                    2f
                );

                // Tailplane
                ImDrawListExtensions.AddLine(
                    draw_list,
                    new float2(center.X - symbolSize * 0.6f, center.Y + symbolSize * 0.6f),
                    new float2(center.X + symbolSize * 0.6f, center.Y + symbolSize * 0.6f),
                    orange,
                    2f
                );
            }

            //// Slip / skid ball at the bottom
            //{
            //    float trackY = center.Y + radius * 0.45f;
            //
            //    // Track line
            //    float2 left = new float2(center.X - ballTrackHalfWidth - 8f, trackY);
            //    float2 right = new float2(center.X + ballTrackHalfWidth + 8f, trackY);
            //    ImDrawListExtensions.AddLine(draw_list, left, right, white, 2f);
            //
            //    // Center reference marks on track
            //    float2 centerMarkLeft = new float2(center.X - 8f, trackY);
            //    float2 centerMarkRight = new float2(center.X + 8f, trackY);
            //    ImDrawListExtensions.AddLine(draw_list, centerMarkLeft, centerMarkRight, white, 1.5f);
            //
            //    // Ball
            //    float2 ballCenter = new float2(center.X + SlipToOffset(filteredSlipAccel), trackY);
            //    ImDrawListExtensions.AddCircleFilled(draw_list, ballCenter, ballRadius, white);
            //}

            // Text readout for turn information
            {
                float2 textPos = new float2(center.X - 40f, center.Y + radius * 0.1f);
                ImDrawListExtensions.AddText(draw_list, textPos, white, rateText);
            }
        }
    }
}
