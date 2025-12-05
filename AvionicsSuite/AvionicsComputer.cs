using Brutal.Numerics;
using KSA;

namespace Avionics {
    internal class AvionicsComputer {
        // Reference to the vehicle and its flight computer
        public Vehicle vehicle;
        public Autopilot autopilot;
        public FlightManagementSystem fms;

        // States of the vehicle need for displaying flight instruments

        // Vehicle info
        public double3 pos_GPS;
        public float heading;
        public float verticalSpeed_mps;
        public float indicatedAirspeed_mps;
        public float radarAltitude;

        // General target info
        public float? targetBearing_rad;
        public float? targetDistance_m;
        public float? targetSlope_rad;
        public float2? targetDeviation;

        // Target info for runway approach
        public double3? targetGPS;

        public AvionicsComputer() {
            autopilot = new Autopilot();
            fms = new FlightManagementSystem();
        }
        public void SetTargetAirport(Airport? airport) {
            fms.targetAirport = airport;
        }
        public void SetTargetRunway(Runway? runway) {
            fms.targetRunway = runway;
        }

        public void Update(float dt) {
            if(vehicle == null || fms == null || autopilot == null) return;

            // Vehicle state update
            UpdateVehicleStateFromSensors(vehicle);

            // FMS update
            fms.Update();

            // Nav updates
            targetGPS = fms.targetRunway?.GetGPS();
            targetBearing_rad = fms.targetRunway != null ? (float)Geomath.GetBearing(pos_GPS, targetGPS.Value) : null;
            targetDistance_m = fms.targetRunway != null ? (float)Geomath.GetDistance(pos_GPS, targetGPS.Value, vehicle.Orbit.Parent.MeanRadius) : null;
            targetSlope_rad = fms.targetRunway != null ? (float)fms.targetRunway.GetCurrentVerticalAngle(pos_GPS, vehicle.Orbit.Parent.MeanRadius) : null;
            float lateralDeviation = fms.targetRunway != null ? (float)fms.targetRunway.GetLateralDeviation(pos_GPS, vehicle.Orbit.Parent.MeanRadius) : new float();
            float verticalDeviation = fms.targetRunway != null ? (float)fms.targetRunway.GetVerticalDeviation(pos_GPS, vehicle.Orbit.Parent.MeanRadius) : new float();
            targetDeviation = fms.targetRunway != null ? new float2(lateralDeviation, verticalDeviation) : null;

            autopilot.Update(vehicle, dt, fms.targetRunway != null ? (float)fms.targetRunway.GetBearing(pos_GPS) : null);
        }
        public void UpdateVehicleStateFromSensors(Vehicle vehicle) {
            pos_GPS = Geomath.GetGPSPosition(vehicle);
            heading = (float)Geomath.GetHeading(vehicle);
            verticalSpeed_mps = (float)double3.Dot(vehicle.GetVelocityCci(), vehicle.GetPositionCci().Normalized());
            indicatedAirspeed_mps = (float)vehicle.GetSurfaceSpeed();
            radarAltitude = (float)vehicle.GetRadarAltitude();
        }
    }
}