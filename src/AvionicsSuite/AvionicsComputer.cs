using Brutal.Numerics;
using KSA;
using System.Drawing;
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
        //public double3? targetGPS;

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
            fd.Update(vehicle, dt, this);

            // Autopilot update
            autopilot.Update(fd);

            // Debug gizmos
            DrawDebugGizmos();
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

        public void DrawDebugGizmos() {
            if(fms.ActivePlan.ActiveLeg == null) return;
            Astronomical parent = (KSA.Astronomical)vehicle.Orbit.Parent;
            if(vehicle.Orbit.Parent is Celestial) {
                Celestial celestial = (Celestial)vehicle.Orbit.Parent;

                // if the fms has a target, draw a line to it
                if(fms.ActivePlan.ActiveLeg.Type == LegType.TrackToFix) {
                    double3 from_posEgo = Geomath.GPSToEGO(fms.ActivePlan.ActiveLeg.From.Gps, celestial, Program.GetMainCamera());
                    double3 to_posEgo = Geomath.GPSToEGO(fms.ActivePlan.ActiveLeg.To.Gps, celestial, Program.GetMainCamera());
                    Program.GizmosRenderer.DrawLine(
                        from_posEgo,
                        to_posEgo,
                        float4.One
                    );
                } else if(fms.ActivePlan.ActiveLeg.Type == LegType.DirectToFix) {
                    double3 from_posEgo = Geomath.GPSToEGO(pos_GPS, celestial, Program.GetMainCamera());
                    double3 to_posEgo = Geomath.GPSToEGO(fms.ActivePlan.ActiveLeg.To.Gps, celestial, Program.GetMainCamera());
                    Program.GizmosRenderer.DrawLine(
                        from_posEgo,
                        to_posEgo,
                        float4.One
                    );
                } else if (fms.ActivePlan.ActiveLeg.Type == LegType.CourseToFix) {
                    // Draw a great-circle arc starting at the active-leg TO waypoint and going halfway around the planet.
                    {
                        if(fms?.ActivePlan?.ActiveLeg?.To == null || fms?.ActivePlan?.ActiveLeg?.CourseRad == null) return;

                        var cam = Program.GetMainCamera();

                        // "To" point (lat, lon in radians; alt in meters)
                        double3 toGps = fms.ActivePlan.ActiveLeg.To.Gps;

                        // Arc settings
                        const int segments = 128;

                        // Start point on unit sphere (ECEF-like: x east-ish, y north-ish, z up/north pole)
                        double3 toUnit = UnitFromGps(toGps);

                        // Desired heading at "to" point (radians)
                        float greatCircleHeading = fms.ActivePlan.ActiveLeg.CourseRad.Value;

                        // IMPORTANT: ensure the heading is radians.
                        // If GetTrueHeading() returns degrees, uncomment the next line and remove the one above:
                        // greatCircleHeading = Geomath.Deg2Rad * fms.ActivePlan.ActiveLeg.AssociatedRunway.GetTrueHeading();

                        double sinLat = Math.Sin(toGps.X);
                        double cosLat = Math.Cos(toGps.X);
                        double sinLon = Math.Sin(toGps.Y);
                        double cosLon = Math.Cos(toGps.Y);

                        // East (unit) and North (unit) in the same frame as UnitFromGps()
                        double3 east = new double3(-sinLon, cosLon, 0.0);
                        double3 north = new double3(-sinLat * cosLon, -sinLat * sinLon, cosLat);

                        // Convert heading (bearing from north, clockwise) to a unit tangent direction
                        double ch = Math.Cos(greatCircleHeading);
                        double sh = Math.Sin(greatCircleHeading);

                        // Tangent direction along the desired great-circle
                        double3 t = Normalize(new double3(
                            north.X * ch + east.X * sh,
                            north.Y * ch + east.Y * sh,
                            north.Z * ch + east.Z * sh
                        ));

                        // If something went weird, fall back to "east"
                        if(LengthSq(t) < 1e-20) t = Normalize(east);

                        // Great-circle plane normal (ensures Cross(n, toUnit) == t)
                        double3 n = Normalize(Cross(toUnit, t));

                        // Re-orthonormalize (optional but tidy)
                        t = -Normalize(Cross(n, toUnit));

                        // March theta from 0..pi (halfway around planet) and draw segments
                        double3 prevEgo = default;
                        bool hasPrev = false;

                        for(int i = 0; i <= segments; i++) {
                            double theta = Math.PI * (double)i / (double)segments;

                            // Point on unit sphere along the great-circle:
                            // u(theta) = toUnit*cos(theta) + t*sin(theta)
                            double c = Math.Cos(theta);
                            double s = Math.Sin(theta);
                            double3 u = new double3(
                                toUnit.X * c + t.X * s,
                                toUnit.Y * c + t.Y * s,
                                toUnit.Z * c + t.Z * s
                            );

                            // Convert back to (lat, lon, alt)
                            double lat = Math.Asin(Clamp(u.Z, -1.0, 1.0));
                            double lon = Math.Atan2(u.Y, u.X);
                            double3 gps = new double3(lat, lon, fms.ActivePlan.ActiveLeg.To.Gps.Z + 6371000.0);

                            // Calculate this distance in meters from (to.X, to.Y) to (gps.X, gps.Y)
                            double3 tempPos = new double3(fms.ActivePlan.ActiveLeg.To.Gps.X, fms.ActivePlan.ActiveLeg.To.Gps.Y, fms.ActivePlan.ActiveLeg.To.Gps.Z + 6371000.0);
                            double distance = Geomath.GetDistance(tempPos, gps);
                            float targetSlope_deg = 3.0f; // typical glideslope angle
                            float targetSlope_rad = Geomath.Deg2Rad * targetSlope_deg;
                            gps = new double3(lat, lon, fms.ActivePlan.ActiveLeg.To.Gps.Z + distance * Math.Tan(targetSlope_rad));

                            // GPS -> EGO and draw
                            double3 ego = Geomath.GPSToEGO(gps, celestial, cam);

                            if(hasPrev) {
                                Program.GizmosRenderer.DrawLine(prevEgo, ego, float4.One);
                            }

                            prevEgo = ego;
                            hasPrev = true;
                        }
                    }

                    // ---- helpers ----

                    static double3 UnitFromGps(double3 gps) {
                        double lat = gps.X;
                        double lon = gps.Y;
                        double clat = Math.Cos(lat);
                        return new double3(
                            clat * Math.Cos(lon),
                            clat * Math.Sin(lon),
                            Math.Sin(lat)
                        );
                    }

                    static double3 Cross(double3 a, double3 b) {
                        return new double3(
                            a.Y * b.Z - a.Z * b.Y,
                            a.Z * b.X - a.X * b.Z,
                            a.X * b.Y - a.Y * b.X
                        );
                    }

                    static double LengthSq(double3 v) => v.X * v.X + v.Y * v.Y + v.Z * v.Z;

                    static double3 Normalize(double3 v) {
                        double ls = LengthSq(v);
                        if(ls <= 0.0) return new double3(0.0, 0.0, 0.0);
                        double inv = 1.0 / Math.Sqrt(ls);
                        return new double3(v.X * inv, v.Y * inv, v.Z * inv);
                    }

                    static double Clamp(double x, double lo, double hi) => (x < lo) ? lo : (x > hi) ? hi : x;

                }
            }
        }
    }
}