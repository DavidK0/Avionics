using Brutal.Numerics;
using KSA;

namespace Avionics {
    internal class FlightDirector {
        public enum VerticalMode {
            AltitudeHold,
            VerticalSpeedHold,
            VNav,
            off
        }
        public enum LateralMode {
            HeadingHold,
            Approach,
            Nav,
            off
        }

        public Vehicle vehicle;

        public VerticalMode verticalMode = VerticalMode.off;
        public LateralMode lateralMode = LateralMode.off;

        public float current_altitude = 10000f;
        public float current_vs;

        public float commanded_vs = 0;

        public float target_vs_mps;
        public float target_altitude_m;

        public UnitController.VariableUnitSmallSpeed target_vs_display_value;
        public float vs_display_mps;

        public UnitController.VariableUnitSmallDistance target_altitude_display_value;
        public float large_height_m;
        public float small_height_m;

        private PID vsPID;
        private PID altitudePID;

        public float commanded_pitch;
        public float commanded_heading;
        public float commanded_roll = 0f;


        private float max_pitch_rad = 0.5f; // ~28.6 degrees

        public FlightDirector() {
            vsPID = new PID(0.3f, 0f, 0.5f);
            altitudePID = new PID(0.1f, 0f, 0.2f);

            target_vs_display_value = new UnitController.VariableUnitSmallSpeed();
            target_altitude_display_value = new UnitController.VariableUnitSmallDistance();
        }

        public void Update(Vehicle v, float dt, float? bearing, NavigationSystem navSystem) {
            if(navSystem == null || v == null) return;

            vehicle = v;
            FlightComputer flightComputer = vehicle.FlightComputer;

            target_altitude_display_value.distance = MathF.Max(0f, target_altitude_display_value.distance);

            // Update target_vs_mps and target_altitude_m based on current unit
            target_altitude_m = target_altitude_display_value.to_SI(); // in meters
            target_vs_mps = target_vs_display_value.to_SI(); // in m/s

            if(UnitController.CurrentUnit == UnitController.UnitSystem.Kilometers) {
                large_height_m = 1000f; // 1000 meters
                small_height_m = 100f; // 100 meters
                vs_display_mps = 1f; // 1 m/s
            } else {
                large_height_m = 1000f / UnitController.m_to_ft; // 1000 feet
                small_height_m = 100f / UnitController.m_to_ft;  // 100 feet
                vs_display_mps = 100f / UnitController.mps_to_fpm; // 100 feet per minute
            }

            // Set pitch
            if(verticalMode == VerticalMode.AltitudeHold) {
                // --- OUTER LOOP: Altitude PID produces a target vertical speed ---
                float altitudeError = target_altitude_m - current_altitude;
                commanded_vs = altitudePID.Update(altitudeError, dt);

                // Clamp commanded vertical speed to target vertical speed
                commanded_vs = Math.Clamp(commanded_vs, -Math.Abs(target_vs_mps), Math.Abs(target_vs_mps));

                // --- INNER LOOP: VS PID produces pitch command ---
                float vsError = commanded_vs - current_vs;
                commanded_pitch = vsPID.Update(vsError, dt);

                commanded_pitch = Math.Clamp(commanded_pitch, -max_pitch_rad, max_pitch_rad);
            } else if(verticalMode == VerticalMode.VerticalSpeedHold) {
                // Vertical speed mode

                // Set commanded vertical speed to target vertical speed
                commanded_vs = target_vs_mps;

                float vsError = commanded_vs - current_vs;
                commanded_pitch = vsPID.Update(vsError, dt);

                commanded_pitch = Math.Clamp(commanded_pitch, -max_pitch_rad, max_pitch_rad);
            } else if(verticalMode == VerticalMode.VNav) {
                if(bearing.HasValue) {
                    // --- OUTER LOOP: Altitude PID produces a target vertical speed ---
                    Console.WriteLine(navSystem.verticalDeviation);
                    Console.WriteLine(GetDebugString());
                    commanded_vs = altitudePID.Update(navSystem.verticalDeviation * -10000f, dt);

                    // Clamp commanded vertical speed to target vertical speed
                    commanded_vs = Math.Clamp(commanded_vs, -Math.Abs(target_vs_mps), Math.Abs(target_vs_mps));

                    // --- INNER LOOP: VS PID produces pitch command ---
                    float vsError = commanded_vs - current_vs;
                    commanded_pitch = vsPID.Update(vsError, dt);

                    commanded_pitch = Math.Clamp(commanded_pitch, -max_pitch_rad, max_pitch_rad);
                } else {
                    verticalMode = VerticalMode.AltitudeHold;
                }
                // Off
                // I don't know how to less than all of the axes right now
            }

            // Set heading
            if(lateralMode == LateralMode.HeadingHold) {
                // Heading hold mode
                commanded_heading = (float)Geomath.GetHeading(vehicle);
            } else if(lateralMode == LateralMode.Approach) {
                // Approach mode
                // To be implemented
                commanded_heading = (float)Geomath.GetHeading(vehicle);
            } else if(lateralMode == LateralMode.Nav) {
                // Nav mode
                if(bearing.HasValue) {
                    commanded_heading = bearing.Value - (float)Math.PI / 2;
                } else {
                    lateralMode = LateralMode.HeadingHold;
                    commanded_heading = (float)Geomath.GetHeading(vehicle);
                }
            } else {
                // Off
                // I don't know how to less than all of the axes right now
                commanded_heading = (float)Geomath.GetHeading(vehicle);
            }
        }

        public string GetDebugString() {
            string vehicleString = vehicle != null ? vehicle.FlightComputer.CustomAttitudeTarget.ToString() : "";
            return $"{target_altitude_m}, {(int)current_altitude}, {target_vs_mps}, {commanded_vs}, {current_vs}, {vehicleString}";
        }
    }
}
