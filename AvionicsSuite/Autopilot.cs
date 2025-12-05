using Brutal.Numerics;
using KSA;

namespace Avionics {
    internal class Autopilot {
        public enum VerticalMode {
            AltitudeHold,
            VerticalSpeed,
            VNAV,
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

        public bool engaged = false;

        public float current_altitude = 10000f;
        public float current_vs;

        public float commanded_vs = 0;

        public float target_vs_mps;
        public float target_altitude_m;

        public float target_vs_display_value;
        public float vs_display_unit;

        public float target_altitude_display_value;
        public float large_height_display_unit;
        public float small_height_display_unit;

        private PID vsPID;
        private PID altitudePID;

        public float commanded_pitch;
        public float commanded_heading;
        public float commanded_roll = 0f;


        private float max_pitch_rad = 0.5f; // ~28.6 degrees

        public Autopilot() {
            vsPID = new PID(0.3f, 0f, 0.5f);
            altitudePID = new PID(0.1f, 0f, 0.2f);
        }

        public void Engage() {
            vsPID.Reset();
            altitudePID.Reset();
            engaged = true;
            verticalMode = VerticalMode.AltitudeHold;
            lateralMode = LateralMode.HeadingHold;
        }

        public void Disengage() {
            engaged = false;
            verticalMode = VerticalMode.off;
            lateralMode = LateralMode.off;
        }

        public void Update(Vehicle v, float dt, float? bearing) {
            vehicle = v;
            FlightComputer flightComputer = vehicle.FlightComputer;

            target_altitude_display_value = MathF.Max(0f, target_altitude_display_value);
            target_vs_display_value = MathF.Max(0f, target_vs_display_value);

            // Update target_vs_mps and target_altitude_m based on current unit
            if(UnitControler.CurrentUnit == UnitControler.UnitSystem.Kilometers) {
                target_altitude_m = target_altitude_display_value; // in meters
                target_vs_mps = target_vs_display_value; // in m/s
            } else {
                target_altitude_m = target_altitude_display_value * 0.3048f; // feet to meters
                target_vs_mps = target_vs_display_value * 0.00508f; // feet per minute to m/s
            }

            if(UnitControler.CurrentUnit == UnitControler.UnitSystem.Kilometers) {
                large_height_display_unit = 1000f; // 1000 meters
                small_height_display_unit = 100f; // 100 meters
                vs_display_unit = 1f; // 1 m/s
            } else {
                large_height_display_unit = 1000f; // 1000 feet
                small_height_display_unit = 100f;  // 100 feet
                vs_display_unit = 100f; // 100 feet per minute
            }

            if(engaged) {
                // Set pitch
                if(verticalMode == VerticalMode.AltitudeHold) {
                    // --- OUTER LOOP: Altitude PID produces a target vertical speed ---
                    float altitudeError = target_altitude_m - current_altitude;
                    commanded_vs = altitudePID.Update(altitudeError, dt);

                    // Clamp commanded vertical speed to sane values
                    commanded_vs = Math.Clamp(commanded_vs, -target_vs_mps, target_vs_mps);

                    // --- INNER LOOP: VS PID produces pitch command ---
                    float vsError = commanded_vs - current_vs;
                    commanded_pitch = vsPID.Update(vsError, dt);

                    commanded_pitch = Math.Clamp(commanded_pitch, -max_pitch_rad, max_pitch_rad);
                } else if(verticalMode == VerticalMode.VerticalSpeed) {
                    // Vertical speed mode
                    // To be implemented
                } else if(verticalMode == VerticalMode.VNAV) {
                    // To be implemented
                } else {
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

                flightComputer.CustomAttitudeTarget = new double3(commanded_roll, commanded_pitch, commanded_heading);
                flightComputer.AttitudeTrackTarget = FlightComputerAttitudeTrackTarget.Custom;
                flightComputer.AttitudeFrame = VehicleReferenceFrame.EnuBody;
            } else {
                flightComputer.CustomAttitudeTarget = new double3(0f, 0f, 0f);
            }

        }

        public string GetDebugString() {
            string vehicleString = vehicle != null ? vehicle.FlightComputer.CustomAttitudeTarget.ToString() : "";
            return $"{engaged}, {target_altitude_m}, {(int)current_altitude}, {target_vs_mps}, {commanded_vs}, {current_vs}, {vehicleString}";
        }

        internal void ChangeUnit(UnitControler.UnitSystem previousUnit) {
            //Console.WriteLine($"Changing unit from {previousUnit} to {UnitControler.CurrentUnit}");
            switch(UnitControler.CurrentUnit) {
                case UnitControler.UnitSystem.NauticalMiles:
                    if(previousUnit == UnitControler.UnitSystem.Kilometers) {
                        // m -> ft
                        target_altitude_display_value *= 3.28084f;

                        // m/s -> ft/min
                        target_vs_display_value *= 196.8504f;

                    }
                    target_vs_display_value = MathF.Round(target_vs_display_value / 100f) * 100f;
                    break;
                case UnitControler.UnitSystem.StatuteMiles:
                    if(previousUnit == UnitControler.UnitSystem.Kilometers) {
                        // m -> ft
                        target_altitude_display_value *= 3.28084f;

                        // m/s -> ft/min
                        target_vs_display_value *= 196.8504f;
                    }
                    target_vs_display_value = MathF.Round(target_vs_display_value / 100f) * 100f;
                    break;
                case UnitControler.UnitSystem.Kilometers:
                    if(previousUnit != UnitControler.UnitSystem.Kilometers) {
                        //ft -> m
                        target_altitude_display_value /= 3.28084f;

                        // ft/min -> m/s
                        target_vs_display_value /= 196.8504f;
                    }
                    target_vs_display_value = MathF.Round(target_vs_display_value);
                    break;
            }

            // Round target_altitude_display_value to the nearest 100 display units
            target_altitude_display_value = MathF.Round(target_altitude_display_value / 100f) * 100f;
        }
    }
    public class PID {
        public float Kp;
        public float Ki;
        public float Kd;

        public float integral;
        public float lastError;

        public PID(float kp, float ki, float kd) {
            Kp = kp;
            Ki = ki;
            Kd = kd;
            Reset();
        }
        public void Reset() {
            integral = 0f;
            lastError = 0f;
        }
        public float Update(float error, float dt) {
            integral += error * dt;
            float derivative = (error - lastError) / dt;

            lastError = error;

            return Kp * error + Ki * integral + Kd * derivative;
        }
        public string GetDebugString() {
            return $"{Kp}, {Ki}, {Kd}, {integral}, {lastError}";
        }
    }
}
