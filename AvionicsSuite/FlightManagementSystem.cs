using Brutal.Numerics;
using System.Text.Json;

namespace Avionics {

    public class FlightManagementSystem {
        public FlightPlan ActivePlan { get; private set; } = new();

        // Derived information for displays
        public float? DistanceToDestinationM { get; private set; }
        public float? EteToDestinationSec { get; private set; }
        public float? TopOfDescentDistanceM { get; private set; }
        public enum LegType {
            TrackToFix,      // straight line between two waypoints
            DirectToFix,     // from present position to a fix
            CourseToFix,     // intercept and follow a course to a fix
            ArcToFix,        // DME arc (optional)
            Hold,            // holding pattern
            Vector           // radar vector (just a heading)
        }
        public enum LegPhase {
            Departure,
            Enroute,
            Arrival,
            Approach,
            MissedApproach,
            Hold
        }

        public class Waypoint {
            public string Name;
            public double3 Gps;
            public float? AltConstraintMsl;
            public float? SpeedConstraint;
        }
        public struct FmsLateralPath {
            public bool IsValid;
            public FlightPlanLeg? Leg;

            public double3? ArcCenterGps;
            public float? ArcRadiusM;
            public float? VectorHeadingRad;
            public float? DesiredTrackRad;
        }
        public struct FmsVerticalPath {
            public bool IsValid;
            public float? TargetAltitudeMsl;
            public float? PathSlopeRad;
        }
        public struct FmsGuidanceSnapshot {
            public FmsLateralPath Lateral;
            public FmsVerticalPath Vertical;
            public bool InHold;
            public bool InApproach;
        }


        public class FlightPlanLeg {
            public LegType Type;
            public LegPhase Phase;
            public Waypoint From;
            public Waypoint To;
            public Runway? AssociatedRunway;

            public float? AtOrAboveAltMsl;
            public float? AtOrBelowAltMsl;
            public float? SpeedLimit;
        }
        public static List<Airport> airports;

        public static void LoadAirportData(string airportDataPath) {
            // Load a list of airports from a file
            var json = File.ReadAllText(airportDataPath);
            var options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            };
            FlightManagementSystem.airports = JsonSerializer.Deserialize<List<Airport>>(json, options);
        }
        public class FlightPlan {
            public List<FlightPlanLeg> Legs { get; } = new();
            public int ActiveLegIndex { get; set; }

            public FlightPlanLeg? ActiveLeg =>
                ActiveLegIndex >= 0 && ActiveLegIndex < Legs.Count
                    ? Legs[ActiveLegIndex]
                    : null;

            public void AdvanceLeg() => ActiveLegIndex++;
        }

        private FmsGuidanceSnapshot _snapshot;

        public void Update(double3 aircraftGps, float groundSpeedMps, float dt) {
            // Leg sequencing, holds, TOD, etc.

            _snapshot = BuildGuidanceSnapshot(aircraftGps);
        }

        public FmsGuidanceSnapshot GetGuidanceSnapshot() => _snapshot;

        public FmsGuidanceSnapshot BuildGuidanceSnapshot(double3 aircraftGps) {
            var snapshot = new FmsGuidanceSnapshot();

            // Default vertical path: none
            snapshot.Vertical = new FmsVerticalPath {
                IsValid = false,
                TargetAltitudeMsl = null,
                PathSlopeRad = null
            };

            snapshot.InHold = false;
            snapshot.InApproach = false;

            var activeLeg = ActivePlan.ActiveLeg;

            if(activeLeg == null) {
                // No active leg: nothing to guide to
                snapshot.Lateral = new FmsLateralPath {
                    IsValid = false,
                    Leg = null,
                    ArcCenterGps = null,
                    ArcRadiusM = null,
                    VectorHeadingRad = null,
                    DesiredTrackRad = null
                };
                return snapshot;
            }

            // There is an active leg
            var leg = activeLeg;

            // Flags derived from leg
            snapshot.InHold = leg.Type == LegType.Hold || leg.Phase == LegPhase.Hold;
            snapshot.InApproach = leg.Phase == LegPhase.Approach || leg.Phase == LegPhase.MissedApproach;

            var lateral = new FmsLateralPath {
                IsValid = true,
                Leg = leg,
                ArcCenterGps = null,
                ArcRadiusM = null,
                VectorHeadingRad = null,
                DesiredTrackRad = null
            };

            // Basic lateral geometry hints per leg type
            switch(leg.Type) {
                case LegType.TrackToFix:
                    // Straight line from From -> To: use that as desired track.
                    // Assumes Geomath.GetBearing(fromGps, toGps) returns a great-circle bearing in radians.
                    lateral.DesiredTrackRad = (float)Geomath.GetBearing(leg.From.Gps, leg.To.Gps);
                    break;

                case LegType.DirectToFix:
                    // Direct-to is usually defined from present position to the TO fix.
                    // We use current aircraft position here.
                    lateral.DesiredTrackRad = (float)Geomath.GetBearing(aircraftGps, leg.To.Gps);
                    break;

                case LegType.CourseToFix:
                    // You probably want an explicit "course" field on FlightPlanLeg at some point.
                    // For now, derive a nominal course from From -> To.
                    lateral.DesiredTrackRad = (float)Geomath.GetBearing(leg.From.Gps, leg.To.Gps);
                    break;

                case LegType.ArcToFix:
                    // DME arc: you’ll likely want to precompute and store ArcCenterGps/ArcRadiusM elsewhere.
                    // For now we just mark the leg as valid and leave geometry hints null.
                    // TODO: fill ArcCenterGps / ArcRadiusM when you add that data to the leg.
                    lateral.DesiredTrackRad = null;
                    break;

                case LegType.Hold:
                    // Proper hold geometry needs inbound course and fix.
                    // When you add hold-specific fields to FlightPlanLeg, wire them here.
                    // For now, treat as a simple course-to-fix using From -> To as a placeholder.
                    lateral.DesiredTrackRad = (float)Geomath.GetBearing(leg.From.Gps, leg.To.Gps);
                    break;

                case LegType.Vector:
                    // Vector is "fly this heading" – but FlightPlanLeg doesn’t expose a heading yet.
                    // Until you add a heading field, you can either:
                    //  - mark lateral invalid so LNAV ignores it, or
                    //  - approximate using From -> To.
                    // Here we approximate.
                    lateral.DesiredTrackRad = (float)Geomath.GetBearing(leg.From.Gps, leg.To.Gps);

                    // Once you add something like leg.VectorHeadingRad:
                    // lateral.VectorHeadingRad = leg.VectorHeadingRad;
                    break;
            }

            snapshot.Lateral = lateral;

            // Simple vertical target from leg constraints.
            // You can replace this later with a proper VNAV path/TOD model.
            float? targetAlt = leg.AtOrBelowAltMsl ?? leg.AtOrAboveAltMsl;
            if(targetAlt.HasValue) {
                var vertical = snapshot.Vertical;
                vertical.IsValid = true;
                vertical.TargetAltitudeMsl = targetAlt;
                vertical.PathSlopeRad = null; // TODO: compute VNAV/glidepath slope when you have a profile
                snapshot.Vertical = vertical;
            }

            return snapshot;
        }

    }
}
