using Brutal.Numerics;
using KSA;
using static Avionics.FlightManagementSystem;

namespace Avionics {
    public class NavigationSystem {
        public enum NavLateralSource {
            None,
            Approach,
            FlightPlan,
            DirectToFix
        }

        public enum NavVerticalSource {
            None,
            GlideSlope,
            VNAV
        }

        public struct NavSolution {
            public bool HasLateralGuidance;
            public bool HasVerticalGuidance;

            public NavLateralSource LateralSource;
            public NavVerticalSource VerticalSource;

            // Geometry
            public double3 TargetGps;
            public float BearingToTarget_rad;      // from aircraft to target
            public double DistanceToTarget_m;

            // Lateral guidance (cross-track error / localizer deviation)
            public float DesiredTrack_rad;         // e.g. runway heading or leg course
            public float CrossTrackError_m;        // >0 = right of course, <0 = left
            public float CrossTrackError_rad;

            // Vertical guidance (glidepath / VNAV path)
            public float DesiredPathSlope_rad;     // e.g. 3° glidepath or VNAV path slope
            public float VerticalPathError_m;      // >0 = above path, <0 = below
            public float VerticalPathError_rad;
        }
        public float planetRadius;

        public NavSolution Current { get; private set; }

        public void Update(double3 aircraftGps,
                           FlightManagementSystem.FmsGuidanceSnapshot fmsGuidance,
                           float planetRadius) {
            this.planetRadius = planetRadius;

            if(fmsGuidance.Lateral.IsValid) {
                Current = ComputePathSolution(aircraftGps, fmsGuidance.Lateral);
            } else {
                Current = default;
            }

            if(fmsGuidance.Vertical.IsValid) {
                // Fill vertical guidance here
            }
        }

        public NavSolution ComputePathSolution(
            double3 aircraftGps,
            FmsLateralPath path) {

            FlightManagementSystem.FlightPlanLeg leg = path.Leg;
            if(!path.IsValid || leg == null) {
                return default;
            }

            switch(path.Leg.Type) {
                case LegType.TrackToFix:
                    return ComputeTrackToFix(aircraftGps, path);
                case LegType.DirectToFix:
                    return ComputeDirectToFix(aircraftGps, path);
                case LegType.CourseToFix:
                    return ComputeCourseToFix(aircraftGps, path);
                case LegType.ArcToFix:
                    return ComputeArcToFix(aircraftGps, path);
                case LegType.Hold:
                    return ComputeHold(aircraftGps, path);
                case LegType.Vector:
                    return ComputeVector(aircraftGps, path);
                default:
                    return default;
            }
        }

        /// <summary>
        /// TrackToFix: fly the great-circle segment from leg.From to leg.To.
        /// CrossTrackError is signed:
        ///   > 0 = aircraft is right of the desired track,
        ///   < 0 = aircraft is left of the desired track,
        /// when looking along DesiredTrack_rad.
        /// </summary>
        public NavSolution ComputeTrackToFix(
            double3 aircraftGps,
            FmsLateralPath path
        ) {
            var leg = path.Leg;
            if(!path.IsValid || leg == null) {
                return default;
            }

            // Waypoints
            var from = leg.From.Gps;
            var to = leg.To.Gps;

            // Target is the TO waypoint
            var targetGps = to;

            // Bearing and distance from aircraft to target
            var bearingToTarget_rad = (float)Geomath.GetBearing(aircraftGps, targetGps);
            var distanceToTarget_m = Geomath.GetDistance(aircraftGps, targetGps, planetRadius);

            // Desired track: prefer what FMS precomputed, otherwise From -> To
            float desiredTrack_rad;
            if(path.DesiredTrackRad.HasValue) {
                desiredTrack_rad = path.DesiredTrackRad.Value;
            } else {
                desiredTrack_rad = (float)Geomath.GetBearing(from, to);
            }

            // Geometry for cross-track error, using standard great-circle math.
            // Distances here are in meters; we convert to angular distance with / planetRadius.
            var distanceFromToAircraft_m = Geomath.GetDistance(from, aircraftGps, planetRadius);
            var bearingFromToAircraft = (float)Geomath.GetBearing(from, aircraftGps);

            // Angular distances (radians)
            var sigma_AP = distanceFromToAircraft_m / planetRadius; // great-circle angle From->Aircraft
            var theta_AB = desiredTrack_rad;                         // course From->To
            var theta_AP = bearingFromToAircraft;                    // course From->Aircraft

            // Cross-track angular error (radians)
            // d_xt = asin( sin(sigma_AP) * sin(theta_AP - theta_AB) )
            var crossTrack_rad = (float)Math.Asin(
                Math.Sin(sigma_AP) * Math.Sin(theta_AP - theta_AB)
            );

            var crossTrack_m = crossTrack_rad * planetRadius;

            // Decide lateral source based on leg phase
            var lateralSource = NavLateralSource.FlightPlan;
            if(leg.Phase == LegPhase.Approach || leg.Phase == LegPhase.MissedApproach) {
                lateralSource = NavLateralSource.Approach;
            }

            var solution = new NavSolution {
                HasLateralGuidance = true,
                HasVerticalGuidance = false, // will be potentially filled in Update

                LateralSource = lateralSource,
                VerticalSource = NavVerticalSource.None,

                TargetGps = targetGps,
                BearingToTarget_rad = bearingToTarget_rad,
                DistanceToTarget_m = distanceToTarget_m,

                DesiredTrack_rad = desiredTrack_rad,
                CrossTrackError_m = crossTrack_m,
                CrossTrackError_rad = crossTrack_rad,

                DesiredPathSlope_rad = 0f,
                VerticalPathError_m = 0f,
                VerticalPathError_rad = 0f
            };

            return solution;
        }
        public NavSolution ComputeDirectToFix(
            double3 aircraftGps,
            FmsLateralPath path) {
            // Not implemented yet
            return default;
        }
        public NavSolution ComputeCourseToFix(
            double3 aircraftGps,
            FmsLateralPath path) {
            // Not implemented yet
            return default;
        }
        public NavSolution ComputeArcToFix(
            double3 aircraftGps,
            FmsLateralPath path) {
            // Not implemented yet
            return default;
        }
        public NavSolution ComputeHold(
            double3 aircraftGps,
            FmsLateralPath path) {
            // Not implemented yet
            return default;
        }
        public NavSolution ComputeVector(
            double3 aircraftGps,
            FmsLateralPath path) {
            // Not implemented yet
            return default;
        }
    }
}
