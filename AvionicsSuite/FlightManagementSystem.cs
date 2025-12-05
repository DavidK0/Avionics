using System;
using System.Collections.Generic;
using System.Text;

namespace Avionics {
    internal class FlightManagementSystem {
        // The FMS stores the flight plan
        public Airport? targetAirport;
        public Runway? targetRunway;
        public FlightManagementSystem() { }
        public void Update() { }
    }
}
