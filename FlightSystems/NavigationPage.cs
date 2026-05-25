using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using KSA;
using RenderCore;
//using Brutal.StbApi.Texture;


namespace Avionics {
    internal static class NavigationPage {
        // --- Persistent view state (kept between frames) ---
        private static bool _initialized;
        private static float _metersPerPixel = 50f;      // zoom: lower = zoom in
        private static float2 _panMeters = default;      // map center in local meters (ENU), relative to aircraft
        private const float MIN_MPP = 0.5f;
        private const float MAX_MPP = 200000f;

        //private static SimpleVkTexture? _vkTex;
        private static VkSampler _sampler;
        private static ImTextureRef _texId;

        //private static SimpleVkTexture? _planetVkTex;
        private static VkSampler _planetSampler;
        private static ImTextureRef _planetTexId;
        private static bool _planetLoaded;

        internal static unsafe void Render(AvionicsComputer avionicsComputer) {
            ImDrawList* draw_list = ImGui.GetWindowDrawList();

            // --- Canvas setup ---
            float2 canvasPos = ImGui.GetCursorScreenPos();
            float2 canvasSize = ImGui.GetContentRegionAvail();
            canvasSize.X = MathF.Max(canvasSize.X, 50);
            canvasSize.Y = MathF.Max(canvasSize.Y, 50);

            // Capture mouse on this region
            ImGui.InvisibleButton("nav_canvas", canvasSize);
            bool hovered = ImGui.IsItemHovered();
            bool active = ImGui.IsItemActive();

            var draw = ImGui.GetWindowDrawList();

            // Background
            uint bg = ImGui.ColorConvertFloat4ToU32(new float4(0.05f, 0.06f, 0.07f, 1f));
            uint border = ImGui.ColorConvertFloat4ToU32(new float4(0.25f, 0.25f, 0.25f, 1f));
            draw.AddRectFilled(canvasPos, canvasPos + canvasSize, bg);
            draw.AddRect(canvasPos, canvasPos + canvasSize, border);

            float2 screenCenter = canvasPos + canvasSize * 0.5f;


            float planetRadius = avionicsComputer.navSystem != null && avionicsComputer.navSystem.planetRadius > 1 ? avionicsComputer.navSystem.planetRadius : 6371000f;

            // Dynamic max zoom out based on current latitude + canvas size
            float maxZoomOutMpp = ComputeMaxMppForPlanet(canvasSize, avionicsComputer.pos_GPS.X, planetRadius);

            // If you still want a global ceiling too:
            float hardMax = MAX_MPP;
            float effectiveMax = MathF.Min(maxZoomOutMpp, hardMax);

            // Keep current zoom valid if latitude changes etc.
            _metersPerPixel = Clamp(_metersPerPixel, MIN_MPP, effectiveMax);

            if(!_initialized) {
                _initialized = true;
                _panMeters = new float2(0, 0); // center on aircraft initially
            }

            // --- Input handling: pan (drag) ---
            var io = ImGui.GetIO();
            if(active && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
                float2 deltaPx = io.MouseDelta;

                // X: dragging right moves map right → world center moves left
                _panMeters.X -= deltaPx.X * _metersPerPixel;

                // Y: dragging down moves map down → world center moves NORTH (+Y)
                _panMeters.Y += deltaPx.Y * _metersPerPixel;

            }

            // --- Input handling: zoom centered on mouse position ---
            if(hovered) {
                float wheel = io.MouseWheel;
                if(MathF.Abs(wheel) > 0.0001f) {
                    float2 mouse = ImGui.GetMousePos();

                    // World point under mouse BEFORE zoom
                    float2 worldBefore = ScreenToWorld(mouse, screenCenter);

                    // Zoom factor (tweak to taste)
                    float zoomFactor = MathF.Pow(0.90f, wheel);   // wheel up => zoom in => mpp decreases
                    float newMpp = Clamp(_metersPerPixel * zoomFactor, MIN_MPP, effectiveMax);

                    if(MathF.Abs(newMpp - _metersPerPixel) > 1e-6f) {
                        _metersPerPixel = newMpp;

                        float2 d = mouse - screenCenter; // screen delta in pixels
                        _panMeters = new float2(
                            worldBefore.X - d.X * _metersPerPixel,
                            worldBefore.Y + d.Y * _metersPerPixel   // note the + (because screen Y is down)
                        );
                    }
                }
            }

            // Aircraft is always at (0,0) in local ENU relative to itself
            float2 acWorld = new float2(0, 0);


            // TO waypoint (if active leg)
            float2? toWorld = null;
            var plan = avionicsComputer.fms?.ActivePlan;
            var legNullable = plan?.ActiveLeg;
            if(legNullable != null) {
                double3 toGps = legNullable.To.Gps;

                // Project GPS delta to local meters relative to aircraft GPS
                toWorld = GpsToLocalMeters(toGps, avionicsComputer.pos_GPS, planetRadius);
            }

            draw.ChannelsSplit(2);

            // Channel 0: background image
            draw.ChannelsSetCurrent(0);
            DrawPlanetMap(draw, canvasPos, canvasSize, avionicsComputer.pos_GPS.X, avionicsComputer.pos_GPS.Y, planetRadius);

            // Channel 1: everything else (grid, rings, markers, text)
            draw.ChannelsSetCurrent(1);

            // After background, after screenCenter computed, before range rings / markers:
            DrawLatLonGrid(draw, canvasPos, canvasSize, screenCenter, avionicsComputer.pos_GPS.X, avionicsComputer.pos_GPS.Y, planetRadius);


            // --- Draw range rings around aircraft ---
            DrawRangeRings(draw, screenCenter, acWorld, canvasSize);

            // --- Draw TO waypoint marker ---
            if(toWorld.HasValue) {
                float2 toScreen = WorldToScreen(toWorld.Value, screenCenter);

                // line from aircraft to TO
                uint lineCol = ImGui.ColorConvertFloat4ToU32(new float4(0.2f, 0.8f, 0.9f, 0.8f));
                float2 acScreen = WorldToScreen(acWorld, screenCenter);
                draw.AddLine(acScreen, toScreen, lineCol, 2f);

                // TO marker: circle + cross
                uint toCol = ImGui.ColorConvertFloat4ToU32(new float4(0.2f, 0.9f, 0.4f, 1f));
                draw.AddCircle(toScreen, 8f, toCol, 0, 2f);
                draw.AddLine(toScreen + new float2(-10, 0), toScreen + new float2(10, 0), toCol, 2f);
                draw.AddLine(toScreen + new float2(0, -10), toScreen + new float2(0, 10), toCol, 2f);

                // label
                draw.AddText(toScreen + new float2(12, -10), toCol, "TO");
            }

            // --- Draw aircraft marker ---
            {
                float2 acScreen = WorldToScreen(acWorld, screenCenter);

                // If your heading is degrees, convert: headingRad = headingDeg * (MathF.PI/180f)
                float headingRad = avionicsComputer.heading;

                DrawPlaneTriangle(draw_list, acScreen, headingRad, 12f);
            }

            // --- Overlay debug ---
            {
                uint txt = ImGui.ColorConvertFloat4ToU32(new float4(1f, 1f, 1f, 0.85f));
                string zoomText = $"Zoom: {_metersPerPixel:0.##} m/px  |  Pan: {_panMeters.X:0}m, {_panMeters.Y:0}m";
                draw.AddText(canvasPos + new float2(8, 8), txt, zoomText);
            }

            draw.ChannelsMerge();
        }
        private const double DEG2RAD = Math.PI / 180.0;
        private const double RAD2DEG = 180.0 / Math.PI;

        //public static void LoadTextureForImGui() {
        //    if(_vkTex != null) return;
        //    var texture = new TextureAsset(
        //        "Content/Core/Textures/Io_Diffuse.jpg",
        //        new(new StbTexture.LoadSettings { ForceChannels = 4 }));
        //
        //    var renderer = KSA.Program.GetRenderer();
        //
        //    using(var stagingPool = renderer.Device.CreateStagingPool(renderer.Graphics, 1))
        //        _vkTex = new SimpleVkTexture(renderer.Device, stagingPool, texture);
        //
        //    // If you won't need the CPU-side asset anymore, you can dispose it here:
        //    // texture.Dispose();
        //
        //    _sampler = renderer.Device.CreateSampler(Presets.Sampler.SamplerPointClamped, null);
        //
        //    _texId = ImGuiBackend.Vulkan.AddTexture(_sampler, _vkTex.ImageView);
        //}

        //public static void Unload() {
        //    var renderer = KSA.Program.GetRenderer();
        //    renderer.Device.WaitIdle();
        //
        //    // Planet map
        //    if(!_planetTexId.Equals(default(ImTextureRef))) {
        //        ImGuiBackend.Vulkan.RemoveTexture(_planetTexId);
        //        _planetTexId = default;
        //    }
        //    if(!_planetSampler.Equals(default(VkSampler))) {
        //        renderer.Device.DestroySampler(_planetSampler, null);
        //        _planetSampler = default;
        //    }
        //    //_planetVkTex = null;
        //    //_planetVkTex?.Dispose();
        //    _planetLoaded = false;
        //
        //    // Other texture (your original)
        //    if(!_texId.Equals(default(ImTextureRef))) {
        //        ImGuiBackend.Vulkan.RemoveTexture(_texId);
        //        _texId = default;
        //    }
        //    if(!_sampler.Equals(default(VkSampler))) {
        //        renderer.Device.DestroySampler(_sampler, null);
        //        _sampler = default;
        //    }
        //    _vkTex?.Dispose();
        //    _vkTex = null;
        //}
        
        //private static void EnsurePlanetMapLoaded() {
        //    if(_planetLoaded) return;
        //
        //    var renderer = Program.GetRenderer();
        //
        //    using var texture = new TextureAsset(
        //        "Content/Core/Textures/Earth_Diffuse.dds",
        //        new(new StbTexture.LoadSettings { ForceChannels = 4 }));
        //
        //    using(var stagingPool = renderer.Device.CreateStagingPool(renderer.Graphics, 1))
        //        _planetVkTex = new SimpleVkTexture(renderer.Device, stagingPool, texture);
        //
        //    _planetSampler = renderer.Device.CreateSampler(Presets.Sampler.SamplerPointClamped, null);
        //    _planetTexId = ImGuiBackend.Vulkan.AddTexture(_planetSampler, _planetVkTex.ImageView);
        //
        //    _planetLoaded = true;
        //}

        private static void DrawPlanetMap(
            ImDrawListPtr draw,
            float2 canvasPos,
            float2 canvasSize,
            double lat0Rad,
            double lon0Rad,
            float planetRadiusM) {
            //EnsurePlanetMapLoaded();
            if(_planetTexId.Equals(default(ImTextureRef))) return;

            // Clip to the canvas
            ImGui.PushClipRect(canvasPos, canvasPos + canvasSize, true);

            // View half-size in meters
            float halfWm = 0.5f * canvasSize.X * _metersPerPixel;
            float halfHm = 0.5f * canvasSize.Y * _metersPerPixel;

            // Screen center is world = _panMeters (east,north) in your transform
            double cosLat0 = Math.Cos(lat0Rad);
            if(Math.Abs(cosLat0) < 1e-6) cosLat0 = 1e-6;

            // Center lat/lon including pan
            double latCRad = lat0Rad + _panMeters.Y / planetRadiusM;
            double lonCRad = lon0Rad + _panMeters.X / (planetRadiusM * cosLat0);

            // Bounds in radians
            double dLat = halfHm / planetRadiusM;
            double dLon = halfWm / (planetRadiusM * cosLat0);

            double latMin = latCRad - dLat;
            double latMax = latCRad + dLat;

            // Clamp latitude to poles
            latMin = Math.Max(latMin, -Math.PI / 2.0);
            latMax = Math.Min(latMax, Math.PI / 2.0);

            double lonMin = lonCRad - dLon;
            double lonMax = lonCRad + dLon;

            // Normalize lon into [-PI, PI)
            lonMin = WrapAngleRad(lonMin);
            lonMax = WrapAngleRad(lonMax);

            // Lat->V (top is +lat)
            float v0 = (float)((Math.PI / 2.0 - latMax) / Math.PI); // top
            float v1 = (float)((Math.PI / 2.0 - latMin) / Math.PI); // bottom

            // If we don't cross the dateline, single draw.
            // If we do cross (lonMin > lonMax after wrap), split into two images.
            if(lonMin <= lonMax) {
                float u0 = (float)((lonMin + Math.PI) / (2.0 * Math.PI));
                float u1 = (float)((lonMax + Math.PI) / (2.0 * Math.PI));

                draw.AddImage(_planetTexId, canvasPos, canvasPos + canvasSize, new float2(u0, v0), new float2(u1, v1));
            } else {
                // Split:
                // [lonMin .. +PI] maps to [uMin .. 1]
                // [-PI .. lonMax] maps to [0 .. uMax]
                float uMin = (float)((lonMin + Math.PI) / (2.0 * Math.PI));
                float uMax = (float)((lonMax + Math.PI) / (2.0 * Math.PI));

                // How much of the view is in the "right side" segment?
                double spanRight = (Math.PI - lonMin);
                double spanTotal = spanRight + (lonMax + Math.PI);
                float rightFrac = (float)(spanRight / spanTotal);

                float2 mid = new float2(canvasPos.X + canvasSize.X * rightFrac, canvasPos.Y);

                // Right segment (lonMin..PI) -> [uMin..1]
                draw.AddImage(_planetTexId,
                    canvasPos,
                    new float2(mid.X, canvasPos.Y + canvasSize.Y),
                    new float2(uMin, v0),
                    new float2(1f, v1));

                // Left segment (-PI..lonMax) -> [0..uMax]
                draw.AddImage(_planetTexId,
                    new float2(mid.X, canvasPos.Y),
                    canvasPos + canvasSize,
                    new float2(0f, v0),
                    new float2(uMax, v1));
            }

            ImGui.PopClipRect();
        }


        private static double WrapAngleRad(double a) {
            // [-PI, PI)
            a = (a + Math.PI) % (2.0 * Math.PI);
            if(a < 0) a += 2.0 * Math.PI;
            return a - Math.PI;
        }

        private static double NiceAngleStepDeg(double deg) {
            // 1-2-5 scaling across decades (supports sub-degree nicely)
            if(deg <= 0) return 1;
            double exp = Math.Floor(Math.Log10(deg));
            double f = deg / Math.Pow(10, exp);

            double nf = (f < 1.5) ? 1.0 : (f < 3.5) ? 2.0 : (f < 7.5) ? 5.0 : 10.0;
            return nf * Math.Pow(10, exp);
        }

        private static void DrawLatLonGrid(
            ImDrawListPtr draw,
            float2 canvasPos,
            float2 canvasSize,
            float2 screenCenter,
            double lat0Rad,
            double lon0Rad,
            float planetRadiusM
        ) {
            // Colors similar to your GroundTrack style: faint minor, stronger major
            uint minorCol = ImGui.ColorConvertFloat4ToU32(new float4(0.8f, 0.8f, 0.8f, 0.12f));
            uint majorCol = ImGui.ColorConvertFloat4ToU32(new float4(0.8f, 0.8f, 0.8f, 0.28f));
            uint textCol = ImGui.ColorConvertFloat4ToU32(new float4(0.9f, 0.9f, 0.9f, 0.75f));

            // 1) Visible world-meter bounds
            float2 w0 = ScreenToWorld(canvasPos, screenCenter);
            float2 w1 = ScreenToWorld(canvasPos + canvasSize, screenCenter);

            float xMin = MathF.Min(w0.X, w1.X);
            float xMax = MathF.Max(w0.X, w1.X);
            float yMin = MathF.Min(w0.Y, w1.Y);
            float yMax = MathF.Max(w0.Y, w1.Y);

            // 2) Convert visible bounds -> approximate lat/lon bounds (equirectangular inverse)
            double cosLat0 = Math.Cos(lat0Rad);
            if(Math.Abs(cosLat0) < 1e-6) cosLat0 = 1e-6; // avoid polar blow-up

            double latMinRad = lat0Rad + (double)yMin / (double)planetRadiusM;
            double latMaxRad = lat0Rad + (double)yMax / (double)planetRadiusM;

            double lonMinRad = lon0Rad + (double)xMin / ((double)planetRadiusM * cosLat0);
            double lonMaxRad = lon0Rad + (double)xMax / ((double)planetRadiusM * cosLat0);

            double latMinDeg = latMinRad * RAD2DEG;
            double latMaxDeg = latMaxRad * RAD2DEG;
            double lonMinDeg = lonMinRad * RAD2DEG;
            double lonMaxDeg = lonMaxRad * RAD2DEG;

            // 3) Choose steps based on zoom: target ~120px between lines
            float targetPx = 120f;
            double stepM = (double)(targetPx * _metersPerPixel);

            double stepLatDeg = NiceAngleStepDeg(stepM / (double)planetRadiusM * RAD2DEG);
            double stepLonDeg = NiceAngleStepDeg(stepM / ((double)planetRadiusM * cosLat0) * RAD2DEG);

            // Keep line count sane
            int maxLines = 80;

            // 4) Draw latitude lines (horizontal)
            {
                double start = Math.Floor(latMinDeg / stepLatDeg) * stepLatDeg;
                double end = Math.Ceiling(latMaxDeg / stepLatDeg) * stepLatDeg;

                int count = 0;
                for(double latDeg = start; latDeg <= end && count < maxLines; latDeg += stepLatDeg, count++) {
                    bool major = (Math.Round(latDeg / stepLatDeg) % 3) == 0; // every 3rd line
                    uint col = major ? majorCol : minorCol;
                    float thick = major ? 2f : 1f;

                    double latRad = latDeg * DEG2RAD;
                    float yNorth = (float)((latRad - lat0Rad) * (double)planetRadiusM);

                    float2 aW = new float2(xMin, yNorth);
                    float2 bW = new float2(xMax, yNorth);
                    float2 aS = WorldToScreen(aW, screenCenter);
                    float2 bS = WorldToScreen(bW, screenCenter);

                    draw.AddLine(aS, bS, col, thick);

                    // Optional labels for major lines
                    if(major) {
                        string txt = $"{latDeg:0.##}°";
                        draw.AddText(new float2(canvasPos.X + 6f, aS.Y - 8f), textCol, txt);
                        float txtW = ImGui.CalcTextSize(txt).X;
                        draw.AddText(new float2(canvasPos.X + canvasSize.X - txtW - 6f, aS.Y - 8f), textCol, txt);
                    }
                }
            }

            // 5) Draw longitude lines (vertical)
            {
                double start = Math.Floor(lonMinDeg / stepLonDeg) * stepLonDeg;
                double end = Math.Ceiling(lonMaxDeg / stepLonDeg) * stepLonDeg;

                int count = 0;
                for(double lonDeg = start; lonDeg <= end && count < maxLines; lonDeg += stepLonDeg, count++) {
                    bool major = (Math.Round(lonDeg / stepLonDeg) % 3) == 0;
                    uint col = major ? majorCol : minorCol;
                    float thick = major ? 2f : 1f;

                    double lonRad = lonDeg * DEG2RAD;
                    double dLon = WrapAngleRad(lonRad - lon0Rad); // IMPORTANT near dateline
                    float xEast = (float)(dLon * cosLat0 * (double)planetRadiusM);

                    float2 aW = new float2(xEast, yMin);
                    float2 bW = new float2(xEast, yMax);
                    float2 aS = WorldToScreen(aW, screenCenter);
                    float2 bS = WorldToScreen(bW, screenCenter);

                    draw.AddLine(aS, bS, col, thick);

                    // Optional label for major lines (top)
                    if(major) {
                        string txt = $"{lonDeg:0.##}°";
                        float txtW = ImGui.CalcTextSize(txt).X;
                        float x = Math.Clamp(aS.X - txtW * 0.5f, canvasPos.X + 2f, canvasPos.X + canvasSize.X - txtW - 2f);
                        draw.AddText(new float2(x, canvasPos.Y + 6f), textCol, txt);
                    }
                }
            }
        }
        private static float ComputeMaxMppForPlanet(float2 canvasSize, double lat0Rad, float planetRadiusM) {
            // Prevent seeing "more than the planet" in either axis.

            double cosLat0 = Math.Cos(lat0Rad);
            if(Math.Abs(cosLat0) < 1e-6) cosLat0 = 1e-6;

            // Half visible span in meters = 0.5 * canvas * mpp
            double halfPxX = 0.5 * Math.Max(1.0, (double)canvasSize.X);
            double halfPxY = 0.5 * Math.Max(1.0, (double)canvasSize.Y);

            // Longitude: allow at most ±180° => dLon <= PI rad
            // xEast = dLon * cos(lat0) * R  => max half-width = PI * cos(lat0) * R
            double maxHalfWidthM = Math.PI * cosLat0 * (double)planetRadiusM;

            // Latitude: restrict to poles relative to current lat0
            // yNorth = dLat * R. To stay within [-PI/2, +PI/2] for latitude:
            double northLimitM = (Math.PI / 2.0 - lat0Rad) * (double)planetRadiusM;
            double southLimitM = (Math.PI / 2.0 + lat0Rad) * (double)planetRadiusM;
            double maxHalfHeightM = Math.Max(0.0, Math.Min(northLimitM, southLimitM)); // symmetric half-span allowed

            // Convert allowed half-span to mpp limit
            double maxMppX = maxHalfWidthM / halfPxX;
            double maxMppY = maxHalfHeightM / halfPxY;

            double maxMpp = Math.Min(maxMppX, maxMppY);

            // Avoid degenerate maxMpp near poles
            if(double.IsNaN(maxMpp) || double.IsInfinity(maxMpp) || maxMpp <= 0.0)
                maxMpp = 1.0;

            return (float)maxMpp;
        }

        // ----------------------------
        // Coordinate transforms
        // ----------------------------
        private static float2 WorldToScreen(float2 worldMeters, float2 screenCenter) {
            // screen = center + (world - pan) / mpp
            float2 v = (worldMeters - _panMeters) / _metersPerPixel;
            return new float2(
                screenCenter.X + v.X,
                screenCenter.Y - v.Y   // <-- flip Y
            );
        }

        private static float2 ScreenToWorld(float2 screenPos, float2 screenCenter) {
            // world = pan + (screen - center) * mpp
            float2 v = screenPos - screenCenter;
            return new float2(
                _panMeters.X + v.X * _metersPerPixel,
                _panMeters.Y - v.Y * _metersPerPixel   // <-- flip Y
            );
        }

        // ----------------------------
        // Simple GPS -> local meters (ENU-ish)
        // Assumes gps = (lat_rad, lon_rad, alt_m).
        // If yours is degrees, convert with:
        //   latRad = latDeg * (PI/180), lonRad = lonDeg * (PI/180)
        // ----------------------------
        private static float2 GpsToLocalMeters(double3 gps, double3 originGps, float planetRadiusM) {
            double lat = gps.X;
            double lon = gps.Y;
            double lat0 = originGps.X;
            double lon0 = originGps.Y;

            double dLat = lat - lat0;
            double dLon = lon - lon0;

            // Equirectangular approximation (good enough for a simple nav page)
            double xEast = dLon * Math.Cos(lat0) * planetRadiusM;
            double yNorth = dLat * planetRadiusM;

            // +X = East, +Y = North
            return new float2((float)xEast, (float)yNorth);
        }

        // ----------------------------
        // Drawing helpers
        // ----------------------------
        private static unsafe void DrawPlaneTriangle(ImDrawList* draw_list, float2 center, float headingRad, float sizePx) {
            // Default triangle pointing up (north), then rotate by heading
            float2 p0 = new float2(0, -sizePx);
            float2 p1 = new float2(-sizePx * 0.6f, sizePx * 0.8f);
            float2 p2 = new float2(sizePx * 0.6f, sizePx * 0.8f);

            p0 = Rotate(p0, headingRad + (float)Math.PI / 2) + center;
            p1 = Rotate(p1, headingRad + (float)Math.PI / 2) + center;
            p2 = Rotate(p2, headingRad + (float)Math.PI / 2) + center;

            uint fill = ImGui.ColorConvertFloat4ToU32(new float4(1f, 0.9f, 0.2f, 1f));
            uint outline = ImGui.ColorConvertFloat4ToU32(new float4(0f, 0f, 0f, 1f));

            ImDrawListExtensions.AddTriangleFilled(draw_list, p0, p1, p2, fill);
            ImDrawListExtensions.AddTriangle(draw_list, p0, p1, p2, outline, 2f);
        }

        private static unsafe void DrawRangeRings(ImDrawList* draw_list, float2 screenCenter, float2 aircraftWorld, float2 canvasSize) {
            float2 acScreen = WorldToScreen(aircraftWorld, screenCenter);

            // displayed radius from aircraft to nearest edge (pixels -> meters)
            float radiusPx = 0.5f * MathF.Min(canvasSize.X, canvasSize.Y);
            float maxRangeM = radiusPx * _metersPerPixel;

            // Choose 3 rings at nice values
            float ringStepM = NiceStep(maxRangeM / 3f);
            if(ringStepM < 1f) ringStepM = 1f;

            uint ringCol = ImGui.ColorConvertFloat4ToU32(new float4(0.8f, 0.8f, 0.8f, 0.25f));
            uint txtCol = ImGui.ColorConvertFloat4ToU32(new float4(0.9f, 0.9f, 0.9f, 0.7f));

            for(int i = 1; i <= 3; i++) {
                float rM = ringStepM * i;
                float rPx = rM / _metersPerPixel;

                ImDrawListExtensions.AddCircle(draw_list, acScreen, rPx, ringCol, 0, 2f);

                // label using your unit system
                string label = UnitController.BigDistanceToString(rM, digits: 1, remove_suffix: false);
                ImDrawListExtensions.AddText(draw_list, acScreen + new float2(rPx + 6f, -8f), txtCol, label);
            }
        }

        private static float NiceStep(float value) {
            // 1-2-5 scaling
            if(value <= 0) return 1;
            float exp = MathF.Floor(MathF.Log10(value));
            float f = value / MathF.Pow(10, exp);

            float nf = (f < 1.5f) ? 1f : (f < 3.5f) ? 2f : (f < 7.5f) ? 5f : 10f;
            return nf * MathF.Pow(10, exp);
        }

        private static float2 Rotate(float2 v, float angleRad) {
            float c = MathF.Cos(angleRad);
            float s = MathF.Sin(angleRad);
            return new float2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        private static float Clamp(float v, float lo, float hi) => (v < lo) ? lo : (v > hi) ? hi : v;
    }
}