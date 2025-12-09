using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace Avionics {
    public static class FlightManagementSystemWindow {
        // Window state
        public static bool pageOn = false;

        // Airport selection / info
        public static ImInputString airport_text_buffer = new ImInputString(9);   // For selecting / viewing an airport
        public static Airport selectedAirport;
        public static Runway selectedRunway;

        // Route building
        private static ImInputString origin_text_buffer = new ImInputString(9);   // For setting route origin
        private static ImInputString waypoint_text_buffer = new ImInputString(9); // For appending new legs

        private static FlightManagementSystem.Waypoint scratchOrigin;             // Origin waypoint for the scratch plan

        // Scratch flight plan (edited in the UI, applied to FMS on EXEC)
        private static FlightManagementSystem.FlightPlan scratchPlan;
        private static bool scratchPlanDirty = false;
        private static int selectedLegIndex = -1;

        public static void Toggle() {
            pageOn = !pageOn;
        }

        public static void Render(AvionicsComputer avionicsComputer) {
            if(!pageOn)
                return;

            Vehicle controlledVehicle = Program.ControlledVehicle;
            if(controlledVehicle == null || avionicsComputer == null || avionicsComputer.fms == null || avionicsComputer.navSystem == null)
                return;

            // Update aircraft state for display
            avionicsComputer.pos_GPS = Geomath.GetGPSPosition(controlledVehicle);
            string positionText = Geomath.GetGPSPositionString(avionicsComputer.pos_GPS);
            string headingText = Geomath.GetHeadingString(avionicsComputer.heading);

            EnsureScratchPlanSynced(avionicsComputer.fms);

            ImGuiWindowFlags flags = ImGuiWindowFlags.None;
            ImGui.Begin("Flight Management System", ref pageOn, flags);

            DrawGuidanceSection(avionicsComputer);
            ImGui.Separator();

            DrawAirportSelectionSection(avionicsComputer, controlledVehicle, positionText, headingText);
            ImGui.Separator();

            DrawFlightPlanEditor(avionicsComputer);
            ImGui.Separator();

            DrawApproachSection(avionicsComputer, controlledVehicle);
            ImGui.Separator();

            DrawExecSection(avionicsComputer);

            ImGui.End();
        }

        // ---------------------------------------------------------------------
        // Sync / cloning helpers
        // ---------------------------------------------------------------------

        private static void EnsureScratchPlanSynced(FlightManagementSystem fms) {
            if(fms == null)
                return;

            // If we don't have a scratch plan yet, or we have no pending edits,
            // clone from the current active plan so the UI reflects what the FMS is flying.
            if(scratchPlan == null || !scratchPlanDirty) {
                scratchPlan = ClonePlan(fms.ActivePlan);
                scratchPlanDirty = false;
            }
        }

        private static FlightManagementSystem.FlightPlan ClonePlan(FlightManagementSystem.FlightPlan src) {
            var dst = new FlightManagementSystem.FlightPlan();
            scratchOrigin = null;

            if(src == null || src.Legs == null)
                return dst;

            foreach(var leg in src.Legs) {
                dst.Legs.Add(CloneLeg(leg));
            }

            if(dst.Legs.Count > 0) {
                // First leg's "From" is treated as origin for UI purposes
                scratchOrigin = dst.Legs[0].From;
            }

            return dst;
        }

        private static FlightManagementSystem.FlightPlanLeg CloneLeg(FlightManagementSystem.FlightPlanLeg src) {
            if(src == null)
                return null;

            var leg = new FlightManagementSystem.FlightPlanLeg {
                Type = src.Type,
                Phase = src.Phase,
                AssociatedRunway = src.AssociatedRunway,
                AtOrAboveAltMsl = src.AtOrAboveAltMsl,
                AtOrBelowAltMsl = src.AtOrBelowAltMsl,
                SpeedLimit = src.SpeedLimit,
                From = CloneWaypoint(src.From),
                To = CloneWaypoint(src.To)
            };

            return leg;
        }

        private static FlightManagementSystem.Waypoint CloneWaypoint(FlightManagementSystem.Waypoint src) {
            if(src == null)
                return null;

            return new FlightManagementSystem.Waypoint {
                Name = src.Name,
                Gps = src.Gps,
                AltConstraintMsl = src.AltConstraintMsl,
                SpeedConstraint = src.SpeedConstraint
            };
        }

        private static void MarkDirty() {
            scratchPlanDirty = true;
        }

        private static void ResetScratchPlan(FlightManagementSystem fms) {
            scratchPlan = ClonePlan(fms.ActivePlan);
            scratchPlanDirty = false;
            selectedLegIndex = -1;
        }

        // ---------------------------------------------------------------------
        // Simple lookup / conversion helpers
        // ---------------------------------------------------------------------

        private static Airport FindAirportByIdent(string ident) {
            if(string.IsNullOrWhiteSpace(ident) || FlightManagementSystem.airports == null)
                return null;

            string trimmed = ident.Trim().ToUpperInvariant();
            return FlightManagementSystem.airports.Find(
                a => a.Ident.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        private static FlightManagementSystem.Waypoint WaypointFromAirport(Airport airport) {
            if(airport == null)
                return null;

            double latRad = Geomath.Deg2Rad * airport.Latitude_deg;
            double lonRad = Geomath.Deg2Rad * airport.longitude_deg;
            double3 gps = new double3(latRad, lonRad, 0);

            return new FlightManagementSystem.Waypoint {
                Name = airport.Ident,
                Gps = gps,
                AltConstraintMsl = null,
                SpeedConstraint = null
            };
        }

        private static FlightManagementSystem.Waypoint WaypointFromRunway(Runway runway) {
            if(runway == null)
                return null;

            double3 gps = runway.GetGPS();
            float thresholdAltM = (float)(runway.Use_LE ? runway.Le_Elevation_m : runway.He_Elevation_m);

            return new FlightManagementSystem.Waypoint {
                Name = runway.GetIdent(),
                Gps = gps,
                AltConstraintMsl = thresholdAltM,
                SpeedConstraint = null
            };
        }

        // ---------------------------------------------------------------------
        // Guidance / status section
        // ---------------------------------------------------------------------

        private static void DrawGuidanceSection(AvionicsComputer avionicsComputer) {
            var fms = avionicsComputer.fms;
            var navSystem = avionicsComputer.navSystem;

            ImGui.Text("Guidance");

            // Route-level info
            if(fms != null && fms.ActivePlan != null && fms.ActivePlan.Legs != null && fms.ActivePlan.Legs.Count > 0) {
                int activeIndex = fms.ActivePlan.ActiveLegIndex;
                if(activeIndex < 0 || activeIndex >= fms.ActivePlan.Legs.Count)
                    activeIndex = 0;

                var activeLeg = fms.ActivePlan.Legs[activeIndex];

                string fromName = activeLeg.From?.Name ?? "?";
                string toName = activeLeg.To?.Name ?? "?";
                ImGui.Text($"Active leg: {activeIndex + 1}/{fms.ActivePlan.Legs.Count}  {fromName} → {toName}");
                if(ImGui.IsItemHovered())
                    ImGui.SetTooltip("Current leg in the active flight plan");

                ImGui.Text($"Phase: {activeLeg.Phase}   Type: {activeLeg.Type}");
            } else {
                ImGui.Text("Active leg: none");
            }

            // FMS snapshot (overall path)
            var snapshot = fms?.GetGuidanceSnapshot() ?? default(FlightManagementSystem.FmsGuidanceSnapshot);
            ImGui.Text($"FMS mode: Lateral={snapshot.Lateral.IsValid}, Vertical={snapshot.Vertical.IsValid}");
            ImGui.Text($"Approach: {(snapshot.InApproach ? "ON" : "OFF")}   Hold: {(snapshot.InHold ? "ON" : "OFF")}");

            // Nav solution (numeric guidance)
            var nav = navSystem?.Current ?? default(NavigationSystem.NavSolution);

            if(nav.HasLateralGuidance) {
                string distToFix = UnitController.BigDistanceToString((float)nav.DistanceToTarget_m, 1);
                string xtk = UnitController.SmallDistanceToString(nav.CrossTrackError_m, 1);
                string dtk = UnitController.RadToString(nav.DesiredTrack_rad, 0, remove_suffix: true);
                string brg = UnitController.RadToString(nav.BearingToTarget_rad, 0, remove_suffix: true);

                ImGui.Text($"To fix: {distToFix}  BRG {brg}  DTK {dtk}");
                if(ImGui.IsItemHovered())
                    ImGui.SetTooltip("Distance and bearing to the active fix and desired track");

                ImGui.Text($"XTK: {xtk}");
                if(ImGui.IsItemHovered())
                    ImGui.SetTooltip("Cross-track error relative to the lateral path");
            } else {
                ImGui.Text("Lateral guidance: none");
            }

            if(nav.HasVerticalGuidance) {
                string vError = UnitController.SmallDistanceToString(nav.VerticalPathError_m, 0);
                ImGui.Text($"Vertical error: {vError}");
                if(ImGui.IsItemHovered())
                    ImGui.SetTooltip("Vertical deviation from VNAV or glideslope path");
            } else {
                ImGui.Text("Vertical guidance: none");
            }

            // Overall flight info (if available)
            if(fms != null) {
                if(fms.DistanceToDestinationM.HasValue) {
                    string distDest = UnitController.BigDistanceToString(fms.DistanceToDestinationM.Value, 1);
                    ImGui.Text($"Distance to destination: {distDest}");
                }

                if(fms.EteToDestinationSec.HasValue) {
                    float eteSec = fms.EteToDestinationSec.Value;
                    int minutes = (int)(eteSec / 60f);
                    int seconds = (int)(eteSec % 60f);
                    ImGui.Text($"ETE to destination: {minutes:00}:{seconds:00}");
                }

                if(fms.TopOfDescentDistanceM.HasValue) {
                    string tod = UnitController.BigDistanceToString(fms.TopOfDescentDistanceM.Value, 1);
                    ImGui.Text($"Top of descent in: {tod}");
                }
            }
        }

        // ---------------------------------------------------------------------
        // Airport selection / info
        // ---------------------------------------------------------------------

        private static void DrawAirportSelectionSection(
            AvionicsComputer avionicsComputer,
            Vehicle controlledVehicle,
            string positionText,
            string headingText) {
            ImGui.Text("Airport selection");

            // Text entry for airport that drives selectedAirport and basic info
            ImGui.Text("Enter airport ICAO");
            ImGui.InputText("Airport ICAO", airport_text_buffer);

            // Keep selectedAirport in sync with text
            UpdateSelectedAirportFromInput();

            if(ImGui.Button("Nearest aerodrome with runway")) {
                if(FlightManagementSystem.airports != null && FlightManagementSystem.airports.Count > 0) {
                    var nearest = Geomath.GetNearestAirport(avionicsComputer.pos_GPS, FlightManagementSystem.airports);
                    if(nearest != null) {
                        selectedAirport = nearest;
                        airport_text_buffer.SetValue(selectedAirport.Ident);
                    }
                }
            }

            ImGui.Text($"Aircraft position: {positionText}");
            ImGui.Text($"Aircraft heading: {headingText}");
            ImGui.Text("");

            string airportName = selectedAirport?.Name ?? "None";
            string airportPosText = "No airport selected";
            string airportBearingText = "No airport selected";
            string airportDistanceText = "No airport selected";

            if(selectedAirport != null) {
                double latRad = Geomath.Deg2Rad * selectedAirport.Latitude_deg;
                double lonRad = Geomath.Deg2Rad * selectedAirport.longitude_deg;
                double3 airportGps = new double3(latRad, lonRad, 0);

                airportPosText = Geomath.GetGPSPositionString(airportGps);

                double bearingRad = Geomath.GetBearing(avionicsComputer.pos_GPS, airportGps);
                airportBearingText = Geomath.GetBearingString((float)bearingRad);

                double distanceM = Geomath.GetDistance(
                    avionicsComputer.pos_GPS,
                    airportGps,
                    controlledVehicle.Orbit.Parent.MeanRadius);

                airportDistanceText = UnitController.BigDistanceToString((float)distanceM);
            }

            ImGui.Text($"Selected airport: {airportName}");
            ImGui.Text($"Airport position: {airportPosText}");
            ImGui.Text($"Bearing to airport: {airportBearingText}");
            ImGui.Text($"Distance to airport: {airportDistanceText}");
        }

        private static void UpdateSelectedAirportFromInput() {
            string entered = airport_text_buffer.ToString().Trim().ToUpperInvariant();
            if(string.IsNullOrEmpty(entered) || FlightManagementSystem.airports == null)
                return;

            var found = FindAirportByIdent(entered);
            if(found != null) {
                selectedAirport = found;

                // Default runway selection if needed
                if(selectedAirport.Runways != null &&
                    selectedAirport.Runways.Count > 0 &&
                    selectedRunway == null) {
                    selectedRunway = selectedAirport.Runways[0];
                }
            }
        }

        // ---------------------------------------------------------------------
        // Flight plan editor (simple: legs between airports, delete, reorder)
        // ---------------------------------------------------------------------

        private static void DrawFlightPlanEditor(AvionicsComputer avionicsComputer) {
            ImGui.Text("Flight plan");

            if(scratchPlan == null) {
                ImGui.Text("No flight plan available.");
                return;
            }

            // Origin field (airport ICAO)
            ImGui.Text("Origin (airport ICAO)");
            ImGui.InputText("Origin", origin_text_buffer);
            ImGui.SameLine();
            if(ImGui.Button("Set origin")) {
                var airport = FindAirportByIdent(origin_text_buffer.ToString());
                if(airport != null) {
                    scratchOrigin = WaypointFromAirport(airport);
                    // Reset plan when changing origin to keep things simple
                    scratchPlan.Legs.Clear();
                    selectedLegIndex = -1;
                    MarkDirty();
                }
            }
            if(ImGui.IsItemHovered())
                ImGui.SetTooltip("Sets the starting airport for the route and clears existing legs");

            // Route summary
            string originName = scratchOrigin?.Name ?? (scratchPlan.Legs.Count > 0 ? scratchPlan.Legs[0].From?.Name : "None");
            string destName = "None";
            if(scratchPlan.Legs.Count > 0) {
                var lastLeg = scratchPlan.Legs[scratchPlan.Legs.Count - 1];
                destName = lastLeg.To?.Name ?? "None";
            }
            ImGui.Text($"Route: {originName} → {destName}");

            // New leg entry (between airports)
            ImGui.Text("Next waypoint (airport ICAO)");
            ImGui.InputText("Next waypoint", waypoint_text_buffer);
            ImGui.SameLine();
            if(ImGui.Button("Append leg")) {
                AppendLegFromAirportInput(avionicsComputer);
            }
            if(ImGui.IsItemHovered())
                ImGui.SetTooltip("Adds a leg from the last waypoint (or origin) to this airport");

            ImGui.Spacing();
            ImGui.Text("Legs:");

            if(scratchPlan.Legs.Count == 0) {
                ImGui.Text("No legs in plan.");
                return;
            }

            // List legs with up/down/delete controls
            for(int i = 0; i < scratchPlan.Legs.Count; i++) {
                var leg = scratchPlan.Legs[i];

                ImGui.PushID(i); // make IDs unique per row

                bool isSelected = (i == selectedLegIndex);
                string label = $"{i + 1}. {leg.From?.Name ?? "?"} → {leg.To?.Name ?? "?"}  [{leg.Phase}/{leg.Type}]";

                if(ImGui.Selectable(label, isSelected)) {
                    selectedLegIndex = i;
                }

                // Second line: controls for this leg
                ImGui.Indent();

                if(ImGui.Button("Up")) {
                    if(i > 0)
                        MoveLeg(i, i - 1);
                }

                ImGui.SameLine();
                if(ImGui.Button("Down")) {
                    if(i < scratchPlan.Legs.Count - 1)
                        MoveLeg(i, i + 1);
                }

                ImGui.SameLine();
                if(ImGui.Button("Delete")) {
                    DeleteLeg(i);
                    ImGui.Unindent();
                    ImGui.PopID();
                    break; // list changed
                }

                ImGui.Unindent();
                ImGui.Spacing();

                ImGui.PopID();
            }
        }

        private static void AppendLegFromAirportInput(AvionicsComputer avionicsComputer) {
            var ident = waypoint_text_buffer.ToString();
            var airport = FindAirportByIdent(ident);
            if(airport == null)
                return;

            if(scratchOrigin == null && scratchPlan.Legs.Count == 0) {
                // If no origin is set yet, use the first airport as origin
                scratchOrigin = WaypointFromAirport(airport);
                MarkDirty();
                return;
            }

            var toWaypoint = WaypointFromAirport(airport);
            FlightManagementSystem.Waypoint fromWaypoint;

            if(scratchPlan.Legs.Count == 0) {
                fromWaypoint = scratchOrigin;
            } else {
                fromWaypoint = scratchPlan.Legs[scratchPlan.Legs.Count - 1].To;
            }

            var leg = new FlightManagementSystem.FlightPlanLeg {
                From = fromWaypoint,
                To = toWaypoint,
                Phase = FlightManagementSystem.LegPhase.Enroute,
                Type = FlightManagementSystem.LegType.TrackToFix,
                AssociatedRunway = null,
                AtOrAboveAltMsl = null,
                AtOrBelowAltMsl = null,
                SpeedLimit = null
            };

            scratchPlan.Legs.Add(leg);
            selectedLegIndex = scratchPlan.Legs.Count - 1;
            MarkDirty();
        }

        private static void MoveLeg(int fromIndex, int toIndex) {
            if(scratchPlan == null || scratchPlan.Legs == null)
                return;

            if(fromIndex < 0 || fromIndex >= scratchPlan.Legs.Count ||
                toIndex < 0 || toIndex >= scratchPlan.Legs.Count ||
                fromIndex == toIndex)
                return;

            var leg = scratchPlan.Legs[fromIndex];
            scratchPlan.Legs.RemoveAt(fromIndex);
            scratchPlan.Legs.Insert(toIndex, leg);
            selectedLegIndex = toIndex;
            MarkDirty();
        }

        private static void DeleteLeg(int index) {
            if(scratchPlan == null || scratchPlan.Legs == null)
                return;

            if(index < 0 || index >= scratchPlan.Legs.Count)
                return;

            scratchPlan.Legs.RemoveAt(index);
            if(scratchPlan.Legs.Count == 0) {
                selectedLegIndex = -1;
            } else {
                selectedLegIndex = Math.Min(index, scratchPlan.Legs.Count - 1);
            }
            MarkDirty();
        }

        // ---------------------------------------------------------------------
        // Approach builder (basic straight-in leg to runway)
        // ---------------------------------------------------------------------

        private static void DrawApproachSection(AvionicsComputer avionicsComputer, Vehicle controlledVehicle) {
            ImGui.Text("Approach");

            // Runway selector based on selectedAirport
            string runwayLabel = selectedRunway != null ? selectedRunway.GetDescription() : "No runway selected";
            ImGui.Text($"Runway: {runwayLabel}");

            if(selectedAirport != null && selectedAirport.Runways != null && selectedAirport.Runways.Count > 0) {
                if(ImGui.BeginCombo("Select runway", runwayLabel)) {
                    foreach(var runway in selectedAirport.Runways) {
                        bool isSelected = (runway == selectedRunway);
                        string desc = runway.GetDescription();
                        if(ImGui.Selectable(desc, isSelected)) {
                            selectedRunway = runway;
                        }

                        if(isSelected)
                            ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }
            } else {
                ImGui.Text("No runways available for this airport.");
            }

            // Append a basic straight-in approach leg to the selected runway
            if(ImGui.Button("Append straight-in approach")) {
                if(selectedRunway != null && scratchPlan != null) {
                    AppendStraightInApproach(avionicsComputer, selectedRunway, controlledVehicle);
                }
            }
            if(ImGui.IsItemHovered())
                ImGui.SetTooltip("Adds a final approach leg from the last route waypoint to the runway threshold");
        }

        private static void AppendStraightInApproach(AvionicsComputer avionicsComputer, Runway runway, Vehicle controlledVehicle) {
            if(scratchPlan == null)
                return;

            var toWaypoint = WaypointFromRunway(runway);
            FlightManagementSystem.Waypoint fromWaypoint;

            if(scratchPlan.Legs.Count > 0) {
                fromWaypoint = scratchPlan.Legs[scratchPlan.Legs.Count - 1].To;
            } else if(scratchOrigin != null) {
                fromWaypoint = scratchOrigin;
            } else {
                // Fall back to current aircraft position if no origin/legs
                fromWaypoint = new FlightManagementSystem.Waypoint {
                    Name = "IF",
                    Gps = avionicsComputer.pos_GPS,
                    AltConstraintMsl = null,
                    SpeedConstraint = null
                };
            }

            var leg = new FlightManagementSystem.FlightPlanLeg {
                From = fromWaypoint,
                To = toWaypoint,
                Phase = FlightManagementSystem.LegPhase.Approach,
                Type = FlightManagementSystem.LegType.TrackToFix,
                AssociatedRunway = runway,
                AtOrAboveAltMsl = null,
                AtOrBelowAltMsl = toWaypoint.AltConstraintMsl,
                SpeedLimit = null
            };

            scratchPlan.Legs.Add(leg);
            selectedLegIndex = scratchPlan.Legs.Count - 1;
            MarkDirty();
        }

        // ---------------------------------------------------------------------
        // EXEC / CANCEL section
        // ---------------------------------------------------------------------

        private static void DrawExecSection(AvionicsComputer avionicsComputer) {
            var fms = avionicsComputer.fms;
            if(fms == null || scratchPlan == null) {
                ImGui.Text("No FMS flight plan to apply.");
                return;
            }

            if(scratchPlanDirty) {
                ImGui.Text("Modifications pending. Press EXEC to apply.");
            } else {
                ImGui.Text("Flight plan is in sync with FMS.");
            }

            if(ImGui.Button("EXEC")) {
                ApplyScratchPlanToFms(fms);
            }
            if(ImGui.IsItemHovered())
                ImGui.SetTooltip("Applies the edited flight plan to the FMS (makes it active)");

            ImGui.SameLine();
            if(ImGui.Button("CANCEL")) {
                ResetScratchPlan(fms);
            }
            if(ImGui.IsItemHovered())
                ImGui.SetTooltip("Discards changes and reloads the active FMS flight plan");
        }

        private static void ApplyScratchPlanToFms(FlightManagementSystem fms) {
            if(fms == null || scratchPlan == null)
                return;

            var activePlan = fms.ActivePlan;
            if(activePlan == null)
                return;

            activePlan.Legs.Clear();

            foreach(var leg in scratchPlan.Legs) {
                activePlan.Legs.Add(CloneLeg(leg));
            }

            activePlan.ActiveLegIndex = 0;
            scratchPlanDirty = false;
        }
    }
}
