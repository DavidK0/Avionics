using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace Avionics {
    internal class AttitudeIndicator {

        // Pitch scale (how many degrees are visible)
        public static float maxPitchDeg = 30f;

        // State
        public static float currentPitchRad;
        public static float currentRollRad;
        public static string attitudeText = "P 0°  R 0°";

        public AttitudeIndicator() {
            // Constructor logic here if needed
        }

        /// <summary>
        /// Update attitude state (pitch, roll) in radians.
        /// Positive pitch = nose up, positive roll = right wing down.
        /// </summary>
        public static void Update(float pitch_rad, float roll_rad) {
            currentPitchRad = pitch_rad;
            currentRollRad = roll_rad;

            float pitchDeg = currentPitchRad * (180f / (float)System.Math.PI);
            float rollDeg = currentRollRad * (180f / (float)System.Math.PI);

            int p = (int)System.MathF.Round(pitchDeg);
            int r = (int)System.MathF.Round(rollDeg);

            // Simple signed text (+/-0°)
            attitudeText = $"P {p:+0;-0;0}°  R {r:+0;-0;0}°";
        }

        /// <summary>
        /// Convert local attitude-space coordinates (+Y up) to screen space,
        /// applying roll rotation and translating to the given center.
        /// </summary>
        public static float2 LocalToWorld(float2 local, float2 center, float cosR, float sinR) {
            float xr = local.X * cosR - local.Y * sinR;
            float yr = local.X * sinR + local.Y * cosR;

            // Local +Y is up, screen +Y is down
            return new float2(center.X + xr, center.Y - yr);
        }

        public static unsafe void Render(ImDrawList* draw_list, float2 windowPos, float2 size) {
            // Center and size
            float radius = Math.Min(size.X, size.Y) * 0.45f;
            float innerRadius = radius * .82f;
            float2 center = new float2(
                windowPos.X + size.X * 0.5f,
                windowPos.Y + size.Y - radius
            );

            ImColor8 white = new ImColor8(255, 255, 255, 255);
            ImColor8 orange = new ImColor8(255, 165, 0, 255);
            ImColor8 sky = new ImColor8(70, 110, 180, 255);
            ImColor8 ground = new ImColor8(150, 90, 40, 255);

            // Build polygon approximating the inner circular area to clip against
            float2[] clipCircle = BuildCircleClipPolygon(center, innerRadius);

            // Bezel (outer ring)
            ImDrawListExtensions.AddCircle(draw_list, center, radius, white, 0, 2f);

            // ---- Attitude sphere transform (pitch + roll) ----

            // Clamp pitch for display so we don't scroll off infinitely
            float pitchDeg = currentPitchRad * (180f / (float)System.Math.PI);
            if(pitchDeg > maxPitchDeg) pitchDeg = maxPitchDeg;
            if(pitchDeg < -maxPitchDeg) pitchDeg = -maxPitchDeg;

            // Pixels per degree of pitch (vertical)
            float pixelsPerDeg = innerRadius / maxPitchDeg;
            // How far the horizon should move in local (+Y up) coordinates
            float verticalOffset = pitchDeg * pixelsPerDeg;

            // Horizon moves opposite to eye: nose up => horizon moves down on screen =>
            // local Y becomes more negative. We apply this as a shift.
            float deltaY = -verticalOffset;

            // Roll
            float cosR = System.MathF.Cos(currentRollRad);
            float sinR = System.MathF.Sin(currentRollRad);

            // Make the sky/ground quads big enough to cover the dial even when banked
            float halfWidth = innerRadius * 2.5f;
            float halfHeight = innerRadius * 2.5f;

            // ---- Draw sky quad (above horizon) ----
            {
                // Local coordinates (+Y up), horizon at y = 0 + deltaY
                float2 skyTL_local = new float2(-halfWidth, 0f + deltaY);
                float2 skyTR_local = new float2(halfWidth, 0f + deltaY);
                float2 skyBR_local = new float2(halfWidth, halfHeight + deltaY);
                float2 skyBL_local = new float2(-halfWidth, halfHeight + deltaY);

                float2 skyTL = LocalToWorld(skyTL_local, center, cosR, sinR);
                float2 skyTR = LocalToWorld(skyTR_local, center, cosR, sinR);
                float2 skyBR = LocalToWorld(skyBR_local, center, cosR, sinR);
                float2 skyBL = LocalToWorld(skyBL_local, center, cosR, sinR);

                // Two triangles to fill the quad, but clipped to the circular region
                ClipAndFillTriangle(draw_list, clipCircle, skyTL, skyTR, skyBR, sky);
                ClipAndFillTriangle(draw_list, clipCircle, skyTL, skyBR, skyBL, sky);
            }

            // ---- Draw ground quad (below horizon) ----
            {
                float2 gTL_local = new float2(-halfWidth, -halfHeight + deltaY);
                float2 gTR_local = new float2(halfWidth, -halfHeight + deltaY);
                float2 gBR_local = new float2(halfWidth, 0f + deltaY);
                float2 gBL_local = new float2(-halfWidth, 0f + deltaY);

                float2 gTL = LocalToWorld(gTL_local, center, cosR, sinR);
                float2 gTR = LocalToWorld(gTR_local, center, cosR, sinR);
                float2 gBR = LocalToWorld(gBR_local, center, cosR, sinR);
                float2 gBL = LocalToWorld(gBL_local, center, cosR, sinR);

                ClipAndFillTriangle(draw_list, clipCircle, gTL, gTR, gBR, ground);
                ClipAndFillTriangle(draw_list, clipCircle, gTL, gBR, gBL, ground);
            }

            // ---- Horizon line ----
            {
                float2 hL_local = new float2(-halfWidth / 2f, 0f + deltaY);
                float2 hR_local = new float2(halfWidth / 2f, 0f + deltaY);

                float2 hL = LocalToWorld(hL_local, center, cosR, sinR);
                float2 hR = LocalToWorld(hR_local, center, cosR, sinR);

                //ImDrawListExtensions.AddLine(draw_list, hL, hR, white, 2f);
            }

            // ---- Pitch ladder (5° steps, major at 10°) ----
            {
                float ladderMax = 45f;
                float ladderStep = 5f;

                for(float markDeg = -ladderMax; markDeg <= ladderMax + 0.1f; markDeg += ladderStep) {
                    // Skip the horizon itself (0°) – already drawn
                    if(System.MathF.Abs(markDeg) < 0.1f)
                        continue;

                    // Where this constant-elevation line appears relative to the center
                    // when the aircraft is at pitchDeg.
                    float yLocal = (markDeg - pitchDeg) * pixelsPerDeg;

                    // Skip marks that are far off-screen
                    if(System.MathF.Abs(yLocal) > innerRadius * 1.0f)
                        continue;

                    bool major = System.MathF.Abs(markDeg % 10f) < 0.1f;
                    float halfLadder = major ? innerRadius * 0.35f : innerRadius * 0.25f;
                    float thickness = major ? 2f : 1f;

                    // Horizontal ladder line in local space
                    float2 leftLocal = new float2(-halfLadder, yLocal);
                    float2 rightLocal = new float2(halfLadder, yLocal);

                    float2 left = LocalToWorld(leftLocal, center, cosR, sinR);
                    float2 right = LocalToWorld(rightLocal, center, cosR, sinR);

                    ImDrawListExtensions.AddLine(draw_list, left, right, white, thickness);

                    // Small vertical stubs at ends (classic "goalpost" style)
                    float stubLength = major ? 6f : 4f;
                    float2 leftStubLocalTop = new float2(-halfLadder, yLocal);
                    float2 leftStubLocalBot = new float2(-halfLadder, yLocal - stubLength);
                    float2 rightStubLocalTop = new float2(halfLadder, yLocal);
                    float2 rightStubLocalBot = new float2(halfLadder, yLocal - stubLength);

                    float2 leftStubTop = LocalToWorld(leftStubLocalTop, center, cosR, sinR);
                    float2 leftStubBot = LocalToWorld(leftStubLocalBot, center, cosR, sinR);
                    float2 rightStubTop = LocalToWorld(rightStubLocalTop, center, cosR, sinR);
                    float2 rightStubBot = LocalToWorld(rightStubLocalBot, center, cosR, sinR);

                    ImDrawListExtensions.AddLine(draw_list, leftStubTop, leftStubBot, white, thickness);
                    ImDrawListExtensions.AddLine(draw_list, rightStubTop, rightStubBot, white, thickness);

                    // Numeric pitch labels at major marks (10°, 20°, 30°)
                    if(major) {
                        string label = System.MathF.Abs(markDeg).ToString("0");

                        float2 leftLabelLocal = new float2(-halfLadder - 16f, yLocal - 2f);
                        float2 rightLabelLocal = new float2(halfLadder + 4f, yLocal - 2f);

                        float2 leftLabel = LocalToWorld(leftLabelLocal, center, cosR, sinR);
                        float2 rightLabel = LocalToWorld(rightLabelLocal, center, cosR, sinR);

                        ImDrawListExtensions.AddText(draw_list, leftLabel, white, label);
                        ImDrawListExtensions.AddText(draw_list, rightLabel, white, label);
                    }
                }
            }

            // ---- Fixed aircraft symbol (miniature airplane) ----
            {
                float wingSpan = innerRadius * 0.9f;
                float fuselageHeight = innerRadius * 0.35f;
                float wingThickness = 3f;
                float fuselageThickness = 2f;

                // Wings
                float2 wingLeft = new float2(center.X - wingSpan * 0.5f, center.Y);
                float2 wingRight = new float2(center.X + wingSpan * 0.5f, center.Y);
                ImDrawListExtensions.AddLine(draw_list, wingLeft, wingRight, orange, wingThickness);

                // Small reference marks at wing tips
                float tipExtent = 10f;
                ImDrawListExtensions.AddLine(
                    draw_list,
                    new float2(wingLeft.X - tipExtent, wingLeft.Y),
                    new float2(wingLeft.X, wingLeft.Y),
                    orange, 2f
                );
                ImDrawListExtensions.AddLine(
                    draw_list,
                    new float2(wingRight.X, wingRight.Y),
                    new float2(wingRight.X + tipExtent, wingRight.Y),
                    orange, 2f
                );

                // Fuselage (vertical line)
                float2 fuselageTop = new float2(center.X, center.Y - fuselageHeight * 0.5f);
                float2 fuselageBottom = new float2(center.X, center.Y + fuselageHeight * 0.5f);
                ImDrawListExtensions.AddLine(draw_list, fuselageTop, fuselageBottom, orange, fuselageThickness);

                // Nose marker (tiny triangle)
                float noseSize = 6f;
                float2 noseTip = new float2(center.X, fuselageTop.Y - noseSize);
                float2 noseLeft = new float2(center.X - noseSize * 0.6f, fuselageTop.Y);
                float2 noseRight = new float2(center.X + noseSize * 0.6f, fuselageTop.Y);
                ImDrawListExtensions.AddTriangleFilled(draw_list, noseTip, noseLeft, noseRight, orange);
            }

            // ---- Simple bank index marks on the bezel ----
            {
                float bankRadius = radius;
                int[] bankMarksDeg = new int[] { -60, -45, -30, -20, -10, 10, 20, 30, 45, 60 };

                foreach(int deg in bankMarksDeg) {
                    // Add the current roll so the whole scale rotates with the gyro
                    float angleRad = deg * Geomath.Deg2Rad - currentRollRad;
                    // If it turns the wrong way in your convention, flip the sign:
                    // float angleRad = deg * Geomath.Deg2Rad - currentRollRad;

                    // 0° at top, + right = clockwise on screen
                    float sinA = System.MathF.Sin(angleRad);
                    float cosA = System.MathF.Cos(angleRad);

                    float tickLen = (System.Math.Abs(deg) == 30 || System.Math.Abs(deg) == 60) ? 10f : 6f;
                    float tickThickness = (System.Math.Abs(deg) == 30 || System.Math.Abs(deg) == 60) ? 2f : 1.5f;

                    float2 outer = new float2(
                        center.X + bankRadius * sinA,
                        center.Y - bankRadius * cosA
                    );
                    float2 inner = new float2(
                        center.X + (bankRadius - tickLen) * sinA,
                        center.Y - (bankRadius - tickLen) * cosA
                    );

                    ImDrawListExtensions.AddLine(draw_list, outer, inner, white, tickThickness);
                }


                // Small fixed triangle at top center (bank reference)
                float topRadius = bankRadius - 4f;
                float2 top = new float2(center.X, center.Y - topRadius);
                float2 topLeft = new float2(center.X - 6f, center.Y - (topRadius - 6f));
                float2 topRight = new float2(center.X + 6f, center.Y - (topRadius - 6f));
                ImDrawListExtensions.AddTriangleFilled(draw_list, top, topLeft, topRight, white);
            }

            // ---- Digital attitude readout at bottom (debug/assist) ----
            {
                float2 textPos = new float2(center.X - radius * 0.7f, center.Y + radius * 0.5f);
                ImDrawListExtensions.AddText(draw_list, textPos, white, attitudeText);
            }
        }
        // How many segments to approximate the circle with
        public static int CircleClipSegments = 64;

        public static float2[] BuildCircleClipPolygon(float2 center, float radius) {
            var pts = new float2[CircleClipSegments];
            float step = 2f * System.MathF.PI / CircleClipSegments;

            // Generate polygon CCW in screen space (Y down → subtract sin)
            for(int i = 0; i < CircleClipSegments; ++i) {
                float a = step * i;
                float ca = System.MathF.Cos(a);
                float sa = System.MathF.Sin(a);
                pts[i] = new float2(
                    center.X + ca * radius,
                    center.Y - sa * radius
                );
            }

            return pts;
        }

        public static float Cross(float2 a, float2 b) {
            return a.X * b.Y - a.Y * b.X;
        }

        public static bool IsInside(float2 p, float2 a, float2 b) {
            // Clip polygon is actually clockwise in screen space, so inside is to the RIGHT of AB
            float2 ab = new float2(b.X - a.X, b.Y - a.Y);
            float2 ap = new float2(p.X - a.X, p.Y - a.Y);
            return Cross(ab, ap) <= 0f;
        }

        public static float2 IntersectSegmentWithLine(float2 p, float2 q, float2 a, float2 b) {
            // intersection of segment PQ with infinite line AB
            float2 r = new float2(q.X - p.X, q.Y - p.Y);
            float2 s = new float2(b.X - a.X, b.Y - a.Y);

            float denom = Cross(r, s);
            if(System.MathF.Abs(denom) < 1e-6f) {
                // nearly parallel, just return p to avoid NaNs
                return p;
            }

            float2 ap = new float2(a.X - p.X, a.Y - p.Y);
            float t = Cross(ap, s) / denom;

            return new float2(
                p.X + r.X * t,
                p.Y + r.Y * t
            );
        }

        public static List<float2> ClipPolygonAgainstConvex(List<float2> subject, float2[] clipPoly) {
            // Sutherland–Hodgman against convex clip polygon
            List<float2> output = new List<float2>(subject);

            for(int i = 0; i < clipPoly.Length; ++i) {
                if(output.Count == 0)
                    break;

                List<float2> input = output;
                output = new List<float2>(input.Count);

                float2 a = clipPoly[i];
                float2 b = clipPoly[(i + 1) % clipPoly.Length];

                float2 s = input[input.Count - 1];
                bool sInside = IsInside(s, a, b);

                for(int j = 0; j < input.Count; ++j) {
                    float2 e = input[j];
                    bool eInside = IsInside(e, a, b);

                    if(eInside) {
                        if(!sInside) {
                            // entering: add intersection then E
                            float2 inter = IntersectSegmentWithLine(s, e, a, b);
                            output.Add(inter);
                        }
                        output.Add(e);
                    } else if(sInside) {
                        // leaving: add intersection only
                        float2 inter = IntersectSegmentWithLine(s, e, a, b);
                        output.Add(inter);
                    }

                    s = e;
                    sInside = eInside;
                }
            }

            return output;
        }

        public static unsafe void ClipAndFillTriangle(
            ImDrawList* draw_list,
            float2[] clipPoly,
            float2 v0, float2 v1, float2 v2,
            ImColor8 col
        ) {
            var subject = new List<float2>(3) { v0, v1, v2 };
            var clipped = ClipPolygonAgainstConvex(subject, clipPoly);

            if(clipped.Count < 3)
                return;

            // Fan triangulation
            float2 origin = clipped[0];
            for(int i = 1; i + 1 < clipped.Count; ++i) {
                ImDrawListExtensions.AddTriangleFilled(draw_list, origin, clipped[i], clipped[i + 1], col);
            }
        }
    }
}
