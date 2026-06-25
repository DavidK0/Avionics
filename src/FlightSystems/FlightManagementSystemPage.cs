using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace Avionics {
    public static class FlightManagementSystemPage {

        // Airport selection / info
        public static ImInputString airport_text_buffer = new ImInputString(9);   // For selecting / viewing an airport
        public static Airport selectedAirport;
        public static Runway selectedRunway;

        // Origin waypoint to use when the plan has no legs yet
        private static FlightManagementSystem.Waypoint scratchOrigin;

        private static int selectedLegIndex = -1;

        public static void Render(AvionicsComputer avionicsComputer) {

            Vehicle controlledVehicle = Program.ControlledVehicle;
            if(controlledVehicle == null || avionicsComputer == null || avionicsComputer.fms == null || avionicsComputer.navSystem == null)
                return;

            // Update aircraft state for display
            avionicsComputer.pos_GPS = Geomath.GetGPSPosition(controlledVehicle);

            DrawAirportSelectionSection(avionicsComputer, controlledVehicle);
            DrawApproachSection(avionicsComputer, controlledVehicle);
            ImGui.Separator();

            DrawFlightPlanEditor(avionicsComputer);
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

        public static FlightManagementSystem.Waypoint WaypointFromRunway(Runway runway, float offset = 0) {
            if(runway == null)
                return null;

            // Threshold position (lat [rad], lon [rad], alt [m])
            double3 thresholdGps = runway.GetGPS();
            float thresholdAltM = (float)(runway.Use_LE ? runway.Le_Elevation_m : runway.He_Elevation_m);

            // No offset -> use the threshold directly
            if(offset == 0f) {
                return new FlightManagementSystem.Waypoint {
                    Name = runway.GetIdent(),
                    Gps = thresholdGps,
                    AltConstraintMsl = thresholdAltM,
                    SpeedConstraint = null
                };
            }

            // Move "offset" meters away from the threshold in the opposite direction
            // of the true heading, along the surface.
            const double earthRadiusM = 6371000.0;   // mean Earth radius in meters

            double lat1 = thresholdGps[0];
            double lon1 = thresholdGps[1];

            double distance = Math.Abs(offset);      // meters, use magnitude only

            // Opposite direction of runway true heading
            double bearing = runway.GetTrueHeading() + Math.PI;
            bearing %= (2.0 * Math.PI);
            if(bearing < 0)
                bearing += 2.0 * Math.PI;

            double angularDistance = distance / earthRadiusM;

            double sinLat1 = Math.Sin(lat1);
            double cosLat1 = Math.Cos(lat1);
            double sinAd = Math.Sin(angularDistance);
            double cosAd = Math.Cos(angularDistance);

            // Destination latitude
            double sinLat2 = sinLat1 * cosAd + cosLat1 * sinAd * Math.Cos(bearing);
            double lat2 = Math.Asin(sinLat2);

            // Destination longitude
            double y = Math.Sin(bearing) * sinAd * cosLat1;
            double x = cosAd - sinLat1 * sinLat2;
            double lon2 = lon1 + Math.Atan2(y, x);

            // Normalize longitude to [-π, π]
            lon2 = (lon2 + Math.PI) % (2.0 * Math.PI);
            if(lon2 < 0)
                lon2 += 2.0 * Math.PI;
            lon2 -= Math.PI;

            // Keep altitude at threshold elevation
            double3 offsetGps = new double3(lat2, lon2, thresholdGps[2]);

            return new FlightManagementSystem.Waypoint {
                Name = runway.GetIdent(),
                Gps = offsetGps,
                AltConstraintMsl = thresholdAltM,
                SpeedConstraint = null
            };
        }

        // ---------------------------------------------------------------------
        // Airport selection / info
        // ---------------------------------------------------------------------

        private static void DrawAirportSelectionSection(
            AvionicsComputer avionicsComputer,
            Vehicle controlledVehicle) {
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
        }

        private static void UpdateSelectedAirportFromInput() {
            string entered = airport_text_buffer.ToString().Trim().ToUpperInvariant();

            // If nothing is entered (or airport list is unavailable), clear selection
            if(string.IsNullOrEmpty(entered) || FlightManagementSystem.airports == null) {
                selectedAirport = null;
                selectedRunway = null;
                return;
            }

            var found = FindAirportByIdent(entered);
            if(found != null) {
                // New valid airport
                selectedAirport = found;

                // Ensure selectedRunway belongs to this airport and exists
                if(selectedAirport.Runways != null && selectedAirport.Runways.Count > 0) {
                    if(selectedRunway == null || !selectedAirport.Runways.Contains(selectedRunway)) {
                        selectedRunway = selectedAirport.Runways[0];
                    }
                } else {
                    // Airport has no runways
                    selectedRunway = null;
                }
            } else {
                // Text does NOT match any airport → clear selection
                selectedAirport = null;
                selectedRunway = null;
            }
        }


        // ---------------------------------------------------------------------
        // Flight plan editor (simple: legs between airports, delete, reorder)
        // ---------------------------------------------------------------------

        private static void DrawFlightPlanEditor(AvionicsComputer avionicsComputer) {
            ImGui.Text("Flight plan");

            var fms = avionicsComputer.fms;
            var plan = fms?.ActivePlan;

            if(plan == null) {
                ImGui.Text("No flight plan available.");
                return;
            }

            // Route summary
            string originName = scratchOrigin?.Name ?? (plan.Legs.Count > 0 ? plan.Legs[0].From?.Name : "None");
            string destName = "None";
            if(plan.Legs.Count > 0) {
                var lastLeg = plan.Legs[plan.Legs.Count - 1];
                destName = lastLeg.To?.Name ?? "None";
            }
            ImGui.Text($"Route: {originName} → {destName}");

            ImGui.Spacing();
            ImGui.Text("Legs:");

            if(plan.Legs.Count == 0) {
                ImGui.Text("No legs in plan.");
                return;
            }

            // List legs with up/down/delete controls
            for(int i = 0; i < plan.Legs.Count; i++) {
                var leg = plan.Legs[i];

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
                        MoveLeg(plan, i, i - 1);
                }

                ImGui.SameLine();
                if(ImGui.Button("Down")) {
                    if(i < plan.Legs.Count - 1)
                        MoveLeg(plan, i, i + 1);
                }

                ImGui.SameLine();
                if(ImGui.Button("Delete")) {
                    DeleteLeg(plan, i);
                    ImGui.Unindent();
                    ImGui.PopID();
                    break; // list changed
                }

                // if the current leg.type == approach, add a button to swap runway direction
                if(leg.Phase == FlightManagementSystem.LegPhase.Approach) {
                    if(leg.AssociatedRunway == null) return;
                    ImGui.SameLine();
                    if(ImGui.Button("Swap Dir")) {
                        leg.AssociatedRunway.Use_LE = !leg.AssociatedRunway.Use_LE;
                        DeleteLeg(plan, i);
                        AppendStraightInApproach(avionicsComputer, leg.AssociatedRunway, avionicsComputer.vehicle);
                        ImGui.Unindent();
                        ImGui.PopID();
                        break; // list changed
                    }
                }

                ImGui.Unindent();
                ImGui.Spacing();

                ImGui.PopID();
            }
        }
        private static void MoveLeg(FlightManagementSystem.FlightPlan plan, int fromIndex, int toIndex) {
            if(plan == null || plan.Legs == null)
                return;

            if(fromIndex < 0 || fromIndex >= plan.Legs.Count ||
                toIndex < 0 || toIndex >= plan.Legs.Count ||
                fromIndex == toIndex)
                return;

            var leg = plan.Legs[fromIndex];
            plan.Legs.RemoveAt(fromIndex);
            plan.Legs.Insert(toIndex, leg);
            selectedLegIndex = toIndex;
        }


        private static void DeleteLeg(FlightManagementSystem.FlightPlan plan, int index) {
            if(plan == null || plan.Legs == null)
                return;

            if(index < 0 || index >= plan.Legs.Count)
                return;

            plan.Legs.RemoveAt(index);
            if(plan.Legs.Count == 0) {
                selectedLegIndex = -1;
            } else {
                selectedLegIndex = Math.Min(index, plan.Legs.Count - 1);
            }
        }



        // ---------------------------------------------------------------------
        // Approach builder (basic straight-in leg to runway)
        // ---------------------------------------------------------------------

        private static void DrawApproachSection(AvionicsComputer avionicsComputer, Vehicle controlledVehicle) {
            ImGui.Text("Approach");

            if(ImGui.Button("Append fly over leg")) {
                if(selectedRunway != null) {
                    AppendFlyOverLeg(avionicsComputer, selectedAirport, controlledVehicle);
                }
            }
            if(ImGui.IsItemHovered())
                ImGui.SetTooltip("Adds a fly over leg from the last route waypoint to the runway threshold");

            // Runway selector based on selectedAirport
            string runwayLabel = selectedRunway != null ? selectedRunway.GetDescription() : "No runway selected";
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
                if(selectedRunway != null) {
                    // Button for swapping runway direction
                    if(ImGui.Button("Swap Runway Direction")) {
                        selectedRunway.Use_LE = !selectedRunway.Use_LE;
                    }
                    ImGui.Text($"Selected runway: {selectedRunway.GetIdent()}");
                }
            } else {
                ImGui.Text("No runways available for this airport.");
            }

            if(selectedRunway != null) {
                if(ImGui.Button("Append straight-in approach")) {
                    AppendStraightInApproach(avionicsComputer, selectedRunway, controlledVehicle);
                }
                if(ImGui.Button("Append Initial Approach Fix (50 nm from threshold)")) {
                    AppendInitialApproachFix(avionicsComputer, selectedRunway, controlledVehicle);
                }
            }
            if(ImGui.IsItemHovered())
                ImGui.SetTooltip("Adds a final approach leg from the last route waypoint to the runway threshold");

        }

        private static void AppendStraightInApproach(AvionicsComputer avionicsComputer, Runway runway, Vehicle controlledVehicle) {
            FlightManagementSystem fms = avionicsComputer.fms;
            FlightManagementSystem.FlightPlan plan = fms?.ActivePlan;
            if(plan == null)
                return;

            FlightManagementSystem.Waypoint toWaypoint = WaypointFromRunway(runway);
            //FlightManagementSystem.Waypoint fromWaypoint;
            //if(plan.Legs.Count > 0) {
            //    fromWaypoint = plan.Legs[plan.Legs.Count - 1].To;
            //} else if(scratchOrigin != null) {
            //    fromWaypoint = scratchOrigin;
            //} else {
            //    // Fall back to current aircraft position if no origin/legs
            //    fromWaypoint = new FlightManagementSystem.Waypoint {
            //        Name = "IF",
            //        Gps = avionicsComputer.pos_GPS,
            //        AltConstraintMsl = null,
            //        SpeedConstraint = null
            //    };
            //}

            var leg = new FlightManagementSystem.FlightPlanLeg {
                CourseRad = (float)runway.GetTrueHeading(),
                To = toWaypoint,
                Phase = FlightManagementSystem.LegPhase.Approach,
                Type = FlightManagementSystem.LegType.CourseToFix,
                AssociatedRunway = runway,
                AtOrAboveAltMsl = null,
                AtOrBelowAltMsl = toWaypoint.AltConstraintMsl,
                SpeedLimit = null
            };

            plan.Legs.Add(leg);
            selectedLegIndex = plan.Legs.Count - 1;
        }

        private static void AppendInitialApproachFix(AvionicsComputer avionicsComputer, Runway runway, Vehicle controlledVehicle) {
            var fms = avionicsComputer?.fms;
            var plan = fms?.ActivePlan;

            // Need a valid plan and airport to do anything
            if(plan == null || runway == null)
                return;

            // Destination is 50 nm from runway threshold along runway heading
            var toWaypoint = WaypointFromRunway(runway, 50f * 1852f);
            if(toWaypoint == null)
                return;

            FlightManagementSystem.Waypoint fromWaypoint;

            // From: last leg's destination if we have legs already
            if(plan.Legs.Count > 0) {
                fromWaypoint = plan.Legs[plan.Legs.Count - 1].To;
            }
            // Or from a scratch origin if one has been set
            else if(scratchOrigin != null) {
                fromWaypoint = scratchOrigin;
            }
            // Otherwise fall back to current aircraft position
            else {
                fromWaypoint = new FlightManagementSystem.Waypoint {
                    Name = "DIR",
                    Gps = avionicsComputer.pos_GPS,
                    AltConstraintMsl = null,
                    SpeedConstraint = null
                };
            }

            var leg = new FlightManagementSystem.FlightPlanLeg {
                From = fromWaypoint,
                To = toWaypoint,
                Phase = FlightManagementSystem.LegPhase.Enroute,           // Not an approach
                Type = FlightManagementSystem.LegType.TrackToFix,          // Simple A->B leg
                AssociatedRunway = null,                                   // Not runway-specific
                AtOrAboveAltMsl = null,
                AtOrBelowAltMsl = null,
                SpeedLimit = null
            };

            plan.Legs.Add(leg);
            selectedLegIndex = plan.Legs.Count - 1;
        }

        private static void AppendFlyOverLeg(AvionicsComputer avionicsComputer, Airport airport, Vehicle controlledVehicle) {
            var fms = avionicsComputer?.fms;
            var plan = fms?.ActivePlan;

            // Need a valid plan and airport to do anything
            if(plan == null || airport == null)
                return;

            // Destination is the selected airport (fly over the field)
            var toWaypoint = WaypointFromAirport(airport);
            if(toWaypoint == null)
                return;

            FlightManagementSystem.Waypoint fromWaypoint;

            // From: last leg's destination if we have legs already
            if(plan.Legs.Count > 0) {
                fromWaypoint = plan.Legs[plan.Legs.Count - 1].To;
            }
            // Or from a scratch origin if one has been set
            else if(scratchOrigin != null) {
                fromWaypoint = scratchOrigin;
            }
            // Otherwise fall back to current aircraft position
            else {
                fromWaypoint = new FlightManagementSystem.Waypoint {
                    Name = "DIR",
                    Gps = avionicsComputer.pos_GPS,
                    AltConstraintMsl = null,
                    SpeedConstraint = null
                };
            }

            var leg = new FlightManagementSystem.FlightPlanLeg {
                From = fromWaypoint,
                To = toWaypoint,
                Phase = FlightManagementSystem.LegPhase.Enroute,           // Not an approach
                Type = FlightManagementSystem.LegType.TrackToFix,          // Simple A->B leg
                AssociatedRunway = null,                                   // Not runway-specific
                AtOrAboveAltMsl = null,
                AtOrBelowAltMsl = null,
                SpeedLimit = null
            };

            plan.Legs.Add(leg);
            selectedLegIndex = plan.Legs.Count - 1;
        }
    }
}