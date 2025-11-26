using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace Avionics {
    internal class FlightInstruments {
        internal enum DistanceUnit {
            Miles,
            Kilometers,
            NauticalMiles
        }
        internal static DistanceUnit CurrentUnit { get; set; } = DistanceUnit.NauticalMiles;

        internal static string DistanceToString(float distanceInMeters, int digits = 0) {
            float value = CurrentUnit switch {
                DistanceUnit.Miles => distanceInMeters / 1609.34f,
                DistanceUnit.Kilometers => distanceInMeters / 1000f,
                DistanceUnit.NauticalMiles => distanceInMeters / 1852f,
                _ => distanceInMeters / 1000f,
            };

            // Build numeric format like "F0", "F1", "F2", etc.
            string format = $"F{digits}";

            return CurrentUnit switch {
                DistanceUnit.Miles => $"{value.ToString(format)} mi",
                DistanceUnit.Kilometers => $"{value.ToString(format)} km",
                DistanceUnit.NauticalMiles => $"{value.ToString(format)} nm",
                _ => $"{value.ToString(format)} km",
            };
        }
        internal static string RadToString(float rad, int digits = 0) {
            float deg = rad * (180.0f / (float)Math.PI);
            string format = $"F{digits}";
            return $"{deg.ToString(format)}°";
        }

        internal static unsafe void RenderHSI(ImDrawList* draw_list, float2 windowPos, float2 size, float heading, Runway? _runway, double2 deviation, float bearing, float distance_m, float slope) {
            if(_runway == null) {
                ImDrawListExtensions.AddText(draw_list, new float2(windowPos.X, windowPos.Y + size.Y * 0.5f), new ImColor8(255, 255, 255, 255), "Select an airport and runway first");

                return;
            }
            Runway runway = _runway;

            // --- Deviation math ------------------------------------------------------
            float lateral_deviation_rad = (float)deviation.X;
            float vertical_deviation_rad = (float)deviation.Y;

            float rad_per_dot  = 1f * ((float)Math.PI / 180.0f);
            float lateral_deviation_dots = lateral_deviation_rad / rad_per_dot;
            float vertical_deviation_dots = vertical_deviation_rad / rad_per_dot;

            // --- Basic CDI geometry --------------------------------------------------
            float cdi_radius = 100.0f;
            float cdi_thickness = 3f;
            float dot_radius = 5.0f;

            // Center of the CDI on screen
            float2 cdi_center = new float2(
                windowPos.X + size.X * 0.5f,
                windowPos.Y + size.Y * 0.5f
            );
            int bearing_deg = (int)(bearing * (180.0f / (float)Math.PI));
            int heading_deg = (int)AvionicsMain.GetHeadingDeg(heading);
            int slope_deg = (int)(slope * (180.0f / (float)Math.PI));
            int slope_int = (int)slope_deg;
            string heading_text = $"{heading_deg}°";
            string bearing_text = $"{bearing_deg}°";
            string distance_text = DistanceToString(distance_m);
            string slope_text = $"{slope_int}°";

            ImDrawListExtensions.AddText(draw_list, cdi_center - new float2(cdi_radius, 20 + cdi_radius), new ImColor8(255, 255, 255, 255), $"HDG:{heading_text}");
            ImDrawListExtensions.AddText(draw_list, cdi_center - new float2(cdi_radius, -cdi_radius), new ImColor8(255, 255, 255, 255), $"BRG:{bearing_text}");
            ImDrawListExtensions.AddText(draw_list, cdi_center - new float2(70 - cdi_radius, 20 + cdi_radius), new ImColor8(255, 255, 255, 255), $"DST:{distance_text}");
            ImDrawListExtensions.AddText(draw_list, cdi_center - new float2(70 - cdi_radius, -cdi_radius), new ImColor8(255, 255, 255, 255), $"Slope:{slope_text}");

            // Transform from CDI-local (0,0 at cdi_center) to world, with rotation
            float2 LocalToWorld(float2 local, float angle = 0f) {
                float cosA = (float)Math.Cos(angle - heading + Math.PI / 2);
                float sinA = (float)Math.Sin(angle - heading + Math.PI / 2);
                float xr = local.X * cosA - local.Y * sinA;
                float yr = local.X * sinA + local.Y * cosA; ;
                return new float2(cdi_center.X + xr, cdi_center.Y + yr);
            }

            ImColor8 white = new ImColor8(255, 255, 255, 255);
            ImColor8 green = new ImColor8(0, 255, 0, 255);

            // --- CDI circle (symmetric; rotation doesn't matter for center) ---------
            //ImDrawListExtensions.AddCircle(draw_list, cdi_center, cdi_radius, white, 0, cdi_thickness);

            // Draw 36 tick marks around the CDI at 10 degree intervals
            float shortTickLength = 8f;
            float longTickLength = 16f;
            for(int i = 0; i < 72; i++) {
                float tickLength;
                float tickThickness;
                if(i % 18 == 0) {
                    tickLength = 12f;
                    tickThickness = 3f;
                } else if(i % 2 == 0) {
                    tickLength = 12f;
                    tickThickness = 2f;
                } else {
                    tickLength = 8f;
                    tickThickness = 1f;
                }
                float angle_rad = i * 5.0f * ((float)Math.PI / 180.0f);
                float2 tick_start_local = new float2(cdi_radius * (float)Math.Cos(angle_rad), cdi_radius * (float)Math.Sin(angle_rad));
                float2 tick_end_local = new float2((cdi_radius - tickLength) * (float)Math.Cos(angle_rad), (cdi_radius - tickLength) * (float)Math.Sin(angle_rad));
                ImDrawListExtensions.AddLine(draw_list, LocalToWorld(tick_start_local), LocalToWorld(tick_end_local), white, tickThickness);
            }

            // Draw a small circle for the north and south markers
            float2 north_local = new float2(0f, cdi_radius);
            float2 south_local = new float2(0f, -cdi_radius);
            float2 west_local = new float2(-cdi_radius, 0f);
            float2 east_local = new float2(cdi_radius, 0f);
            ImDrawListExtensions.AddText(draw_list, LocalToWorld(north_local), white, "N");
            ImDrawListExtensions.AddText(draw_list, LocalToWorld(south_local), white, "S");
            ImDrawListExtensions.AddText(draw_list, LocalToWorld(west_local), white, "E");
            ImDrawListExtensions.AddText(draw_list, LocalToWorld(east_local), white, "W");

            // --- Deviation dots (defined in local CDI coordinates) -------------------
            float r1 = cdi_radius * 0.33f;
            float r2 = cdi_radius * 0.66f;

            // Lateral deviation dots (left/right)
            float2 left_dot1_local = new float2(-r1, 0.0f);
            float2 left_dot2_local = new float2(-r2, 0.0f);
            float2 right_dot1_local = new float2(r1, 0.0f);
            float2 right_dot2_local = new float2(r2, 0.0f);

            ImDrawListExtensions.AddCircle(draw_list, LocalToWorld(left_dot1_local, (float)runway.GetTrueHeading()), dot_radius, white, 0);
            ImDrawListExtensions.AddCircle(draw_list, LocalToWorld(left_dot2_local, (float)runway.GetTrueHeading()), dot_radius, white, 0);
            ImDrawListExtensions.AddCircle(draw_list, LocalToWorld(right_dot1_local, (float)runway.GetTrueHeading()), dot_radius, white, 0);
            ImDrawListExtensions.AddCircle(draw_list, LocalToWorld(right_dot2_local, (float)runway.GetTrueHeading()), dot_radius, white, 0);

            // --- Deviation needles (also defined in local CDI coordinates) ----------
            float lateral_deviation_pixels = lateral_deviation_dots * cdi_radius / 3.0f;

            // Lateral deviation needle (nominally vertical line in local space)
            for(int i = 0; i < 3; i++) {
                float2 lateral_needle_p1_local = new float2(lateral_deviation_pixels / (float)Math.Pow(10, i), -cdi_radius * .5f);
                float2 lateral_needle_p2_local = new float2(lateral_deviation_pixels / (float)Math.Pow(10, i), cdi_radius * .5f);
                float2 lateral_needle_p1 = LocalToWorld(lateral_needle_p1_local, (float)runway.GetTrueHeading());
                float2 lateral_needle_p2 = LocalToWorld(lateral_needle_p2_local, (float)runway.GetTrueHeading());
                ImDrawListExtensions.AddLine(draw_list, lateral_needle_p1, lateral_needle_p2, green, i * 2);
            }

            // Draw runway alignment needle
            float2 alignment_needle_p1_local = new float2(0, cdi_radius);
            float2 alignment_needle_p2_local = new float2(0, cdi_radius * .55f);
            float2 alignment_needle_p3_local = new float2(0, -cdi_radius * .55f);
            float2 alignment_needle_p4_local = new float2(0, -cdi_radius);
            float2 alignment_needle_1_p1 = LocalToWorld(alignment_needle_p2_local, (float)runway.GetTrueHeading());
            float2 alignment_needle_1_p2 = LocalToWorld(alignment_needle_p1_local, (float)runway.GetTrueHeading());
            float2 alignment_needle_2_p1 = LocalToWorld(alignment_needle_p4_local, (float)runway.GetTrueHeading());
            float2 alignment_needle_2_p2 = LocalToWorld(alignment_needle_p3_local, (float)runway.GetTrueHeading());
            ImDrawListExtensions.AddLine(draw_list, alignment_needle_1_p1, alignment_needle_1_p2, green, 1);
            ImDrawListExtensions.AddLine(draw_list, alignment_needle_2_p1, alignment_needle_2_p2, green, 1);
            // Arrowhead
            float2 arrowhead_p1_local = new float2(0, cdi_radius * .95f);
            float2 arrowhead_p2_local = new float2(5, cdi_radius * .75f);
            float2 arrowhead_p3_local = new float2(-5, cdi_radius * .75f);
            float2 arrowhead_p1 = LocalToWorld(arrowhead_p1_local, (float)runway.GetTrueHeading());
            float2 arrowhead_p2 = LocalToWorld(arrowhead_p2_local, (float)runway.GetTrueHeading());
            float2 arrowhead_p3 = LocalToWorld(arrowhead_p3_local, (float)runway.GetTrueHeading());
            ImDrawListExtensions.AddTriangleFilled(draw_list, arrowhead_p1, arrowhead_p2, arrowhead_p3, green);
            
            // Draw bearing marker
            float2 bearing_marker_p1_local = new float2(-5, cdi_radius - longTickLength * 2);
            float2 bearing_marker_p2_local = new float2(0, cdi_radius - longTickLength);
            float2 bearing_marker_p3_local = new float2(5, cdi_radius - longTickLength * 2);
            float2 bearing_marker_p4_local = new float2(5, -cdi_radius + longTickLength);
            float2 bearing_marker_p5_local = new float2(-5, -cdi_radius + longTickLength);
            float2 bearing_marker_p1 = LocalToWorld(bearing_marker_p1_local, bearing);
            float2 bearing_marker_p2 = LocalToWorld(bearing_marker_p2_local, bearing);
            float2 bearing_marker_p3 = LocalToWorld(bearing_marker_p3_local, bearing);
            float2 bearing_marker_p4 = LocalToWorld(bearing_marker_p4_local, bearing);
            float2 bearing_marker_p5 = LocalToWorld(bearing_marker_p5_local, bearing);
            ImDrawListExtensions.AddLine(draw_list, bearing_marker_p1, bearing_marker_p2, white, 1);
            ImDrawListExtensions.AddLine(draw_list, bearing_marker_p2, bearing_marker_p3, white, 1);
            ImDrawListExtensions.AddLine(draw_list, bearing_marker_p3, bearing_marker_p4, white, 1);
            ImDrawListExtensions.AddLine(draw_list, bearing_marker_p4, bearing_marker_p5, white, 1);
            ImDrawListExtensions.AddLine(draw_list, bearing_marker_p5, bearing_marker_p1, white, 1);






            void RenderGlideSlopeIndicator(float2 pMin, float2 pMax) {
                // Draw a box for the glide slope indicator
                ImDrawListExtensions.AddRect(draw_list, pMin, pMax, white, 0, ImDrawFlags.None, 1f);

                void DrawDiamond(float2 center, ImColor8 color, float size = 5f) {
                    float2 top = new float2(center.X, center.Y - size);
                    float2 right = new float2(center.X + size, center.Y);
                    float2 bottom = new float2(center.X, center.Y + size);
                    float2 left = new float2(center.X - size, center.Y);
                    ImDrawListExtensions.AddLine(draw_list, top, right, color, 1f);
                    ImDrawListExtensions.AddLine(draw_list, right, bottom, color, 1f);
                    ImDrawListExtensions.AddLine(draw_list, bottom, left, color, 1f);
                    ImDrawListExtensions.AddLine(draw_list, left, top, color, 1f);
                }

                float2 glide_slope_indicator_center = new float2((pMin.X + pMax.X) * 0.5f, (pMin.Y + pMax.Y) * 0.5f);
                float glide_slope_indicator_height = pMax.Y - pMin.Y;

                // Vertial deviation dots
                float2 top_dot1 = new float2(0f, glide_slope_indicator_height  / 6f);
                float2 top_dot2 = new float2(0f, glide_slope_indicator_height / 3f);
                float2 bottom_dot1 = new float2(0f, -glide_slope_indicator_height / 6f);
                float2 bottom_dot2 = new float2(0f, -glide_slope_indicator_height / 3f);
                DrawDiamond(glide_slope_indicator_center + top_dot1, white);
                DrawDiamond(glide_slope_indicator_center + top_dot2, white);
                DrawDiamond(glide_slope_indicator_center + bottom_dot1, white);
                DrawDiamond(glide_slope_indicator_center + bottom_dot2, white);

                // Vertical deviation marker
                float vertical_deviation_pixels = vertical_deviation_dots * glide_slope_indicator_height / 6f;
                float2 deviation_marker_center = new float2(glide_slope_indicator_center.X, glide_slope_indicator_center.Y + vertical_deviation_pixels);
                DrawDiamond(deviation_marker_center, green, 7f);
            }

            float gs_indicator_width = 25f;
            float2 pMin1 = new float2(windowPos.X + size.X * .5f + cdi_radius + 5, windowPos.Y + size.Y * 0.5f - cdi_radius);
            float2 pMax1 = new float2(windowPos.X + size.X * .5f + cdi_radius + 5 + gs_indicator_width, windowPos.Y + size.Y * 0.5f + cdi_radius);
            float2 pMin2 = new float2(windowPos.X + size.X * .5f - cdi_radius - 5 - gs_indicator_width, windowPos.Y + size.Y * 0.5f - cdi_radius);
            float2 pMax2 = new float2(windowPos.X + size.X * .5f - cdi_radius - 5, windowPos.Y + size.Y * 0.5f + cdi_radius);
            // Render two identical glide slope indicators, one on each side of the CDI
            RenderGlideSlopeIndicator(pMin1, pMax1);
            RenderGlideSlopeIndicator(pMin2, pMax2);
        }
    }
}
