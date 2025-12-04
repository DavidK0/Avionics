using Brutal.Numerics;

namespace Avionics {
    internal class FlightInstruments {
        internal enum DistanceUnit {
            StatuteMiles, // miles + feet-per-minute
            Kilometers,   // km + meters-per-second
            NauticalMiles // nm + feet-per-minute
        }
        internal static DistanceUnit CurrentUnit { get; set; } = DistanceUnit.NauticalMiles;
        public static event Action? OnChangeUnit;
        internal static class Units {
            private const float MetersPerStatuteMile = 1609.34f;
            private const float MetersPerKilometer = 1000f;
            private const float MetersPerNauticalMile = 1852f;
            private const float FeetPerMeter = 3.28084f;
            private const float MetersPerFoot = 0.3048f;

            internal static (float factor, string suffix) GetDistance(float meters, DistanceUnit unit) => unit switch {
                DistanceUnit.StatuteMiles => (meters / MetersPerStatuteMile, "mi"),
                DistanceUnit.Kilometers => (meters / MetersPerKilometer, "km"),
                DistanceUnit.NauticalMiles => (meters / MetersPerNauticalMile, "nm"),
                _ => (meters / MetersPerKilometer, "km"),
            };
            internal static (float factor, string suffix) GetAltitude(float meters, DistanceUnit unit, bool use_small_units = false) {
                if(use_small_units) {
                    return unit switch {
                        DistanceUnit.StatuteMiles => (meters * FeetPerMeter, "ft"),
                        DistanceUnit.Kilometers => (meters, "m"),
                        DistanceUnit.NauticalMiles => (meters * FeetPerMeter, "ft"),
                        _ => (meters, "m"),
                    };
                } else {
                    return unit switch {
                        DistanceUnit.StatuteMiles => (meters / MetersPerFoot / 1000, "ftx1000"),
                        DistanceUnit.Kilometers => (meters / MetersPerKilometer, "km"),
                        DistanceUnit.NauticalMiles => (meters / MetersPerFoot / 1000, "ftx1000"),
                        _ => (meters / MetersPerKilometer, "km"),
                    };
                }
            }

            internal static (float factor, string suffix) GetVerticalSpeed(float mps, DistanceUnit unit) => unit switch {
                DistanceUnit.Kilometers => (mps, "mps"),
                DistanceUnit.StatuteMiles => (mps * FeetPerMeter * 60f, "fpm"),
                DistanceUnit.NauticalMiles => (mps * FeetPerMeter * 60f, "fpm"),
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
            float deg = rad * AvionicsMain.Rad2Deg;
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

        internal static void ChangeUnit(DistanceUnit newUnit) {
            CurrentUnit = newUnit;
            OnChangeUnit?.Invoke();
        }
    }
}
