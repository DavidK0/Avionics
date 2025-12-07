using Brutal.Numerics;
using KSA;

namespace Avionics {
    internal class Autopilot {
        internal bool engaged = false;
        private Vehicle vehicle;

        public Autopilot(Vehicle vehicle) {
            this.vehicle = vehicle;
        } 
        public void Engage() {
            engaged = true;
        }

        public void Disengage() {
            engaged = false;
            vehicle.FlightComputer.CustomAttitudeTarget = new double3(0f, 0f, 0f);
        }
        internal void Update(FlightDirector fd) {
            if(!engaged)
                return;
            FlightComputer flightComputer = vehicle.FlightComputer;
            flightComputer.CustomAttitudeTarget = new double3(fd.commanded_roll, fd.commanded_pitch, fd.commanded_heading);
            flightComputer.AttitudeTrackTarget = FlightComputerAttitudeTrackTarget.Custom;
            flightComputer.AttitudeFrame = VehicleReferenceFrame.EnuBody;
        }
    }
}