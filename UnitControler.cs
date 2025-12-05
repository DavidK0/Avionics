namespace Avionics {
    internal class UnitControler {
        internal enum UnitSystem {
            StatuteMiles, // miles + feet-per-minute
            Kilometers,   // km + meters-per-second
            NauticalMiles // nm + feet-per-minute
        }
        internal static UnitSystem CurrentUnit { get; set; } = UnitSystem.NauticalMiles;
        public static event Action<UnitSystem>? OnChangeUnit;
        internal static class Units {
            private const float MetersPerStatuteMile = 1609.34f;
            private const float MetersPerKilometer = 1000f;
            private const float MetersPerNauticalMile = 1852f;
            private const float FeetPerMeter = 3.28084f;
            private const float MetersPerFoot = 0.3048f;

            internal static (float value, string suffix) GetDistance(float meters, UnitSystem unit) => unit switch {
                UnitSystem.StatuteMiles => (meters / MetersPerStatuteMile, "mi"),
                UnitSystem.Kilometers => (meters / MetersPerKilometer, "km"),
                UnitSystem.NauticalMiles => (meters / MetersPerNauticalMile, "nm"),
                _ => (meters / MetersPerKilometer, "km"),
            };
            internal static (float factor, string suffix) GetAltitude(float meters, UnitSystem unit, bool use_small_units = false) {
                if(use_small_units) {
                    return unit switch {
                        UnitSystem.StatuteMiles => (meters * FeetPerMeter, "ft"),
                        UnitSystem.Kilometers => (meters, "m"),
                        UnitSystem.NauticalMiles => (meters * FeetPerMeter, "ft"),
                        _ => (meters, "m"),
                    };
                } else {
                    return unit switch {
                        UnitSystem.StatuteMiles => (meters / MetersPerFoot / 1000, "ftx1000"),
                        UnitSystem.Kilometers => (meters / MetersPerKilometer, "km"),
                        UnitSystem.NauticalMiles => (meters / MetersPerFoot / 1000, "ftx1000"),
                        _ => (meters / MetersPerKilometer, "km"),
                    };
                }
            }

            internal static (float factor, string suffix) GetVerticalSpeed(float mps, UnitSystem unit) => unit switch {
                UnitSystem.Kilometers => (mps, "mps"),
                UnitSystem.StatuteMiles => (mps * FeetPerMeter * 60f, "fpm"),
                UnitSystem.NauticalMiles => (mps * FeetPerMeter * 60f, "fpm"),
                _ => (mps, "mps"),
            };
        }

        internal static string DistanceToString(float distanceInMeters, int digits = 0, bool remove_suffix = false) {
            var (value, suffix) = Units.GetDistance(distanceInMeters, CurrentUnit);
            if(remove_suffix) {
                return $"{value.ToString($"F{digits}")}";
            } else {
                return $"{value.ToString($"F{digits}")} {suffix}";
            }
        }

        internal static string SpeedToString(float speedInMps, int digits = 0, bool remove_suffix = false) {
            var (value, suffix) = Units.GetVerticalSpeed(speedInMps, CurrentUnit);
            if(remove_suffix) {
                return $"{value.ToString($"F{digits}")}";
            } else {
                return $"{value.ToString($"F{digits}")} {suffix}";
            }
        }

        internal static string RadToString(float rad, int digits = 0, bool remove_suffix = false) {
            float deg = rad * Geomath.Rad2Deg;
            string format = $"F{digits}";
            if(remove_suffix) {
                return $"{deg.ToString(format)}";
            } else {
                return $"{deg.ToString(format)}°";
            }
        }

        internal static string AltitudeToString(float altitudeInMeters, int digits = 0, bool remove_suffix = false, bool use_small_units = false) {
            var (value, suffix) = Units.GetAltitude(altitudeInMeters, CurrentUnit, use_small_units);
            if(remove_suffix) {
                return $"{value.ToString($"F{digits}")}";
            } else {
                return $"{value.ToString($"F{digits}")} {suffix}";
            }
        }

        internal static void ChangeUnit(UnitSystem newUnit) {
            UnitSystem previousUnit = CurrentUnit;
            //Console.WriteLine($"Changing unit from {previousUnit} to {CurrentUnit}");
            CurrentUnit = newUnit;
            //Console.WriteLine($"Changing unit from {previousUnit} to {CurrentUnit}");
            OnChangeUnit?.Invoke(previousUnit); // pass the previous unit
        }
    }
}
