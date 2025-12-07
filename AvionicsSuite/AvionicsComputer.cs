using Brutal.Numerics;
using KSA;

namespace Avionics {
    internal class AvionicsComputer {
        // Reference to the vehicle and its flight computer
        public Vehicle vehicle;
        public FlightManagementSystem fms;
        public NavigationSystem navSystem;
        public FlightDirector fd;
        public Autopilot autopilot;

        // States of the vehicle need for displaying flight instruments

        // Vehicle info
        public double3 pos_GPS;
        public float radarAltitude;
        // Speeds
        public float indicatedAirspeed_mps;
        public float verticalSpeed_mps;
        public float lateralSpeed_mps;
        // Acceleration
        public float previous_lateralSpeed_mps;
        public float lateral_acceleration;
        // Heading
        public float heading;
        public float previous_heading;
        public float yawRate;

        // Target info for runway approach
        public double3? targetGPS;

        public AvionicsComputer(Vehicle vehicle) {
            this.vehicle = vehicle;
            fms = new FlightManagementSystem();
            navSystem = new NavigationSystem();
            fd = new FlightDirector();
            autopilot = new Autopilot(vehicle);
        }
        public void SetApproach(Runway runway) {
            fms.ActiveApproach = new FlightManagementSystem.ApproachProcedure(runway);
        }

        public void Update(float dt) {
            if(vehicle == null || fms == null || fd == null) return;

            // Vehicle state update
            UpdateVehicleStateFromSensors(vehicle);

            // Acceleration
            lateral_acceleration = (indicatedAirspeed_mps - previous_lateralSpeed_mps) / dt; // m/s²
            previous_lateralSpeed_mps = lateralSpeed_mps;

            // Yaw rate
            yawRate = (heading - previous_heading) / dt;
            previous_heading = heading;

            // Flight Management System update
            fms.Update(dt);

            // Navigation system update
            if(fms.ActiveApproach != null)
                navSystem.Update(pos_GPS, fms.ActiveApproach, (float)vehicle.Parent.MeanRadius);

            // Flight director update
            fd.Update(vehicle, dt, navSystem.targetBearing_rad, navSystem);

            // Autopilot update
            autopilot.Update(fd);
        }
        public void UpdateVehicleStateFromSensors(Vehicle vehicle) {
            pos_GPS = Geomath.GetGPSPosition(vehicle);
            heading = (float)Geomath.GetHeading(vehicle);
            radarAltitude = (float)vehicle.GetRadarAltitude();
            // Speeds
            verticalSpeed_mps = (float)double3.Dot(vehicle.GetVelocityCci(), vehicle.GetPositionCci().Normalized());
            indicatedAirspeed_mps = (float)vehicle.GetSurfaceSpeed();
            lateralSpeed_mps = (float)Math.Sqrt(indicatedAirspeed_mps * indicatedAirspeed_mps - verticalSpeed_mps * verticalSpeed_mps);
        }
    }
}