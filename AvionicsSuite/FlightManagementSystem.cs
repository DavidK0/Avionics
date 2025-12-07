using Brutal.Numerics;
using System.Text.Json;

namespace Avionics {

    internal class FlightManagementSystem {
        public FlightPlan ActivePlan { get; private set; } = new();
        public ApproachProcedure? ActiveApproach { get; set; }
        public HoldPattern? ActiveHold { get; private set; }

        // Derived information for displays
        public float? DistanceToDestinationM { get; private set; }
        public float? EteToDestinationSec { get; private set; }
        public float? TopOfDescentDistanceM { get; private set; }
        internal enum LegType {
            TrackToFix,      // straight line between two waypoints
            DirectToFix,     // from present position to a fix
            CourseToFix,     // intercept and follow a course to a fix
            ArcToFix,        // DME arc (optional)
            Hold,            // holding pattern
            Vector           // radar vector (just a heading)
        }

        internal struct Waypoint {
            public string Name;
            public double3 Gps;
            public float? AltConstraintMsl;
            public float? SpeedConstraint;
        }

        internal struct FlightPlanLeg {
            public LegType Type;
            public Waypoint From;
            public Waypoint To;

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
        internal class FlightPlan {
            public List<FlightPlanLeg> Legs { get; } = new();
            public int ActiveLegIndex { get; private set; }

            public FlightPlanLeg? ActiveLeg =>
                ActiveLegIndex >= 0 && ActiveLegIndex < Legs.Count
                    ? Legs[ActiveLegIndex]
                    : (FlightPlanLeg?)null;

            public void AdvanceLeg() => ActiveLegIndex++;
        }
        internal class ApproachProcedure {
            public Runway Runway;
            public ApproachProcedure(Runway runway) {
                Runway = runway;
            }
        }
        internal struct HoldPattern {
            public Waypoint Fix;
            public float InboundCourseDeg;
            public bool RightTurns;
            public float LegTimeSec;   // or distance-based leg
        }

        internal enum HoldState { None, Entry, Inbound, Outbound, Turn }
        public void Update(float dt) {
            // - Monitor leg completion & sequencing
            // - Manage holds (enter / exit)
            // - Compute TOD based on altitude and descent profile
            // - Provide current nav target to NavigationComputer
        }
    }
}
