using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace Avionics {
    internal class HeadingIndicator {
        // Geometry
        static private float radius = 100f;
        static private float innerRadius = radius - 18f;

        // State
        static private float currentHeadingRad;
        static private string headingText = "0°";

        public HeadingIndicator() {
            // Constructor logic here
        }

        public static void Update(float heading_rad) {
            currentHeadingRad = heading_rad;

            // Convert to degrees for display (0-360)
            int headingDeg = (int)(Geomath.GetHeadingDeg(heading_rad)) % 360;
            if (headingDeg < 0) headingDeg += 360;
            headingText = $"{headingDeg:D3}°";
        }

        /// <summary>
        /// Transforms a local coordinate to world (screen) space, rotating by the current heading.
        /// </summary>
        static private float2 LocalToWorld(float2 local, float2 center) {
            // Rotate opposite to heading so compass card appears to rotate under fixed aircraft symbol
            float angle = -currentHeadingRad - (float)Math.PI / 2f;
            float cosA = MathF.Cos(angle);
            float sinA = MathF.Sin(angle);

            float xr = local.X * cosA - local.Y * sinA;
            float yr = local.X * sinA + local.Y * cosA;

            return new float2(center.X + xr, center.Y + yr);
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

            // Draw rotating compass card with tick marks every 5 degrees
            for (int deg = 0; deg < 360; deg += 5) {
                float angleRad = deg * Geomath.Deg2Rad;

                // Determine tick length and thickness based on degree
                float tickLength;
                float tickThickness;
                if (deg % 30 == 0) {
                    // Major tick every 30 degrees
                    tickLength = 16f;
                    tickThickness = 2.5f;
                } else if (deg % 10 == 0) {
                    // Medium tick every 10 degrees
                    tickLength = 12f;
                    tickThickness = 2f;
                } else {
                    // Minor tick every 5 degrees
                    tickLength = 8f;
                    tickThickness = 1f;
                }

                // Calculate tick positions in local space (0° = up/north)
                float2 outerLocal = new float2(
                    radius * MathF.Sin(angleRad),
                    -radius * MathF.Cos(angleRad)
                );
                float2 innerLocal = new float2(
                    (radius - tickLength) * MathF.Sin(angleRad),
                    -(radius - tickLength) * MathF.Cos(angleRad)
                );

                // Transform to screen space with heading rotation
                float2 outerWorld = LocalToWorld(outerLocal, center);
                float2 innerWorld = LocalToWorld(innerLocal, center);

                ImDrawListExtensions.AddLine(draw_list, outerWorld, innerWorld, white, tickThickness);

                // Draw cardinal/intercardinal labels at 30-degree intervals
                if (deg % 30 == 0) {
                    string label = deg switch {
                        0 => "N",
                        30 => "3",
                        60 => "6",
                        90 => "E",
                        120 => "12",
                        150 => "15",
                        180 => "S",
                        210 => "21",
                        240 => "24",
                        270 => "W",
                        300 => "30",
                        330 => "33",
                        _ => ""
                    };

                    float labelRadius = radius - 28f;
                    float2 labelLocal = new float2(
                        labelRadius * MathF.Sin(angleRad),
                        -labelRadius * MathF.Cos(angleRad)
                    );
                    float2 labelWorld = LocalToWorld(labelLocal, center);

                    // Offset for text centering
                    float2 textOffset = new float2(-4f, -6f);
                    if (label.Length > 1) textOffset.X = -8f;

                    ImColor8 labelColor = (deg == 0 || deg == 90 || deg == 180 || deg == 270) ? yellow : white;
                    ImDrawListExtensions.AddText(draw_list, labelWorld + textOffset, labelColor, label);
                }
            }

            // Draw fixed aircraft symbol / lubber line at top (current heading indicator)
            float lubberLength = 20f;
            float2 lubberTop = new float2(center.X, center.Y - radius + 2f);
            float2 lubberBottom = new float2(center.X, center.Y - radius + lubberLength);
            ImDrawListExtensions.AddLine(draw_list, lubberTop, lubberBottom, orange, 3f);

            // Draw small triangle at lubber line
            float triangleSize = 8f;
            float2 triTop = new float2(center.X, center.Y - radius + lubberLength + 2f);
            float2 triLeft = new float2(center.X - triangleSize, center.Y - radius + lubberLength + triangleSize + 2f);
            float2 triRight = new float2(center.X + triangleSize, center.Y - radius + lubberLength + triangleSize + 2f);
            ImDrawListExtensions.AddTriangleFilled(draw_list, triTop, triLeft, triRight, orange);

            // Draw center aircraft symbol
            float symbolSize = 12f;
            // Fuselage
            ImDrawListExtensions.AddLine(draw_list, 
                new float2(center.X, center.Y - symbolSize), 
                new float2(center.X, center.Y + symbolSize * 0.5f), 
                orange, 2f);
            // Wings
            ImDrawListExtensions.AddLine(draw_list, 
                new float2(center.X - symbolSize * 1.2f, center.Y), 
                new float2(center.X + symbolSize * 1.2f, center.Y), 
                orange, 2f);
            // Tail
            ImDrawListExtensions.AddLine(draw_list, 
                new float2(center.X - symbolSize * 0.5f, center.Y + symbolSize * 0.5f), 
                new float2(center.X + symbolSize * 0.5f, center.Y + symbolSize * 0.5f), 
                orange, 2f);

            // Digital heading readout at bottom
            float2 textPos = new float2(center.X - 50f, center.Y + 10f);
            ImDrawListExtensions.AddText(draw_list, textPos, white, $"HDG: {headingText}");
        }
    }
}
