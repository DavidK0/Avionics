using Brutal.Numerics;
using KSA;
using static Avionics.FlightManagementSystem;

namespace Avionics {
    public class AvionicsComputer {
        // Reference to the vehicle and its flight computer
        public Vehicle vehicle;
        public FlightManagementSystem fms;
        public NavigationSystem navSystem;
        public FlightDirector fd;
        public Autopilot autopilot;

        // States of the vehicle need for displaying flight instruments

        // Vehicle info from sensors
        public double3 pos_GPS;
        public float roll;
        public float pitch;
        public float heading;
        public float radarAltitude;
        public float angle_of_attack;
        public float angle_of_sideslip;
        public float indicatedAirspeed_mps;
        public float verticalSpeed_mps;

        public double gMag;
        public float slipAccelBodyY;

        // Secondary calculated states
        public float previous_heading;
        public float yawRate;
        public float slipDeflection;
        public double3 PrevVelCci;

        // Target info for runway approach
        public double3? targetGPS;

        public AvionicsComputer(Vehicle vehicle) {
            this.vehicle = vehicle;
            fms = new FlightManagementSystem();
            navSystem = new NavigationSystem();
            fd = new FlightDirector();
            autopilot = new Autopilot(vehicle);
        }

        public void Update(float dt) {
            if(vehicle == null || fms == null || fd == null) return;
            FmsGuidanceSnapshot snapshot = fms.GetGuidanceSnapshot();

            // Vehicle state update
            UpdateVehicleStateFromSensors(vehicle);

            // Secondary calculations
            SecondaryCalculations(dt);

            // Flight Management System update
            // TODO: change indicated airspeed to ground speed
            fms.Update(pos_GPS, indicatedAirspeed_mps, dt);

            // Navigation system update
            navSystem.Update(pos_GPS, snapshot, (float)vehicle.Parent.MeanRadius);

            // Flight director update
            fd.Update(vehicle, dt, navSystem);

            // Autopilot update
            autopilot.Update(fd);
        }
        public void UpdateVehicleStateFromSensors(Vehicle vehicle) {
            pos_GPS = Geomath.GetGPSPosition(vehicle);

            roll = (float)Geomath.GetSurfaceAttitude(vehicle).X;
            pitch = (float)Geomath.GetSurfaceAttitude(vehicle).Y;
            heading = (float)Geomath.GetSurfaceAttitude(vehicle).Z;

            radarAltitude = (float)vehicle.GetRadarAltitude();

            verticalSpeed_mps = (float)double3.Dot(vehicle.GetVelocityCci(), vehicle.GetPositionCci().Normalized());
            indicatedAirspeed_mps = (float)vehicle.GetSurfaceSpeed();
        }

        public void SecondaryCalculations(float dt) {
            // Yaw rate
            yawRate = (heading - previous_heading) / dt;
            previous_heading = heading;

            doubleQuat cci2body = vehicle.GetBody2Cci().Inverse();
            double3 velCce = vehicle.GetVelocityCce();
            double3 velCci = (VehicleReferenceFrameEx.GetEnu2Cci(vehicle.GetPositionCci()) ?? doubleQuat.Identity) * velCce;
            double3 velBody = cci2body * velCci;

            // AoA
            angle_of_attack = (float)Math.Atan2(-velBody.X, -velBody.Z);

            // Slip angle
            SetSlipBallDeflection(dt);
        }
        public void SetSlipBallDeflection(double dt) {
            if(vehicle == null || dt <= 0.0)
                return;

            // 1) Inertial acceleration (CCI)
            var state = vehicle.Orbit.StateVectors;

            double3 velCciNow = state.VelocityCci;
            double3 dVelCci = velCciNow - PrevVelCci;
            double3 accCci = dVelCci / dt;

            PrevVelCci = velCciNow;

            // 2) Transform acceleration to BODY frame
            doubleQuat cci2body = vehicle.GetBody2Cci().Inverse();
            double3 accBody = cci2body * accCci;

            // 3) Gravity vector in CCI (radial)
            double3 posCci = state.PositionCci;
            double r = posCci.Length();

            double3 gBody = double3.Zero;
            double gMag = 0.0;

            if(vehicle.Orbit.Parent is Celestial celestial) {
                double mu = 6.6743e-11 * celestial.Mass;   // or celestial.Mu if available
                gMag = mu / (r * r);

                // Toward planet center
                double3 gCci = -posCci / r * gMag;

                // Convert to BODY frame
                gBody = cci2body * gCci;
            } else {
                // Fallback Earth gravity
                gMag = 9.81;

                // In BRUTAL body frame, +Z = down
                // So gravity points +Z in body frame:
                gBody = new double3(0, 0, gMag);
            }

            // 4) Apparent gravity in BODY frame
            double3 gAppBody = gBody - accBody;

            // 5) Slip ball quantity: lateral apparent acceleration
            slipAccelBodyY = (float)gAppBody.Y;   // raw lateral force

            if(gMag <= 1e-6)
                return;

            // 6) Normalized slip-ball deflection (≈ -1 .. +1)
            slipDeflection = (float)(gAppBody.Y / gMag);
        }
    }
}