namespace Avionics {
    internal static class UnitController {
        internal enum UnitSystem {
            StatuteMiles, // miles, ft, feet-per-minute, and mph
            Kilometers,   // kilometers, meters, meters-per-second, and kph
            NauticalMiles // nm, meters, feet-per-minute, and knots
        }
        internal static UnitSystem CurrentUnit { get; set; } = UnitSystem.NauticalMiles;
        private delegate void DistanceSubscriber(Func<float, float> converter);

        private static readonly List<DistanceSubscriber> bigDistance_subscribers = new();
        private static readonly List<DistanceSubscriber> smallDistance_subscribers = new();
        private static readonly List<DistanceSubscriber> bigSpeed_subscribers = new();
        private static readonly List<DistanceSubscriber> smallSpeed_subscribers = new();

        internal const float mi_to_m = 1609.34f;
        internal const float km_to_m = 1000f;
        internal const float nm_to_m = 1852f;
        internal const float m_to_ft = 3.28084f;
        internal const float mi_to_km = 1.60934f;
        internal const float nm_to_km = 1.852f;
        internal const float nm_to_mi = 1.15078f;
        internal const float mps_to_fpm = 196.850394f;
        internal const float mps_to_kph = 3.6f;
        internal const float mps_to_mph = 2.23694f;
        internal const float mps_to_knt = 1.94384f;

        internal static class Units {
            

            internal static (float value, string suffix) GetBigDistance(float meters, UnitSystem unit) => unit switch {
                UnitSystem.StatuteMiles => (meters / mi_to_m, "mi"),
                UnitSystem.Kilometers => (meters / km_to_m, "km"),
                UnitSystem.NauticalMiles => (meters / nm_to_m, "nm"),
                _ => (meters / km_to_m, "km"),
            };
            internal static (float value, string suffix) GetSmallDistance(float meters, UnitSystem unit) {
                return unit switch {
                    UnitSystem.StatuteMiles => (meters * m_to_ft, "ft"),
                    UnitSystem.Kilometers => (meters, "m"),
                    UnitSystem.NauticalMiles => (meters * m_to_ft, "ft"),
                    _ => (meters, "m"),
                };
            }

            internal static (float value, string suffix) GetSmallSpeed(float mps, UnitSystem unit) => unit switch {
                UnitSystem.Kilometers => (mps, "mps"),
                UnitSystem.StatuteMiles => (mps * m_to_ft * 60f, "fpm"),
                UnitSystem.NauticalMiles => (mps * m_to_ft * 60f, "fpm"),
                _ => (mps, "mps"),
            };
        }

        internal static string BigDistanceToString(float distanceInMeters, int digits = 0, bool remove_suffix = false) {
            var (value, suffix) = Units.GetBigDistance(distanceInMeters, CurrentUnit);
            if(remove_suffix) {
                return $"{value.ToString($"F{digits}")}";
            } else {
                return $"{value.ToString($"F{digits}")} {suffix}";
            }
        }

        internal static string SmallSpeedToString(float speedInMps, int digits = 0, bool remove_suffix = false) {
            var (value, suffix) = Units.GetSmallSpeed(speedInMps, CurrentUnit);
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

        internal static string SmallDistanceToString(float altitudeInMeters, int digits = 0, bool remove_suffix = false) {
            var (value, suffix) = Units.GetSmallDistance(altitudeInMeters, CurrentUnit);
            if(remove_suffix) {
                return $"{value.ToString($"F{digits}")}";
            } else {
                return $"{value.ToString($"F{digits}")} {suffix}";
            }
        }

        internal static void ChangeUnit(UnitSystem newUnit) {
            var previousUnit = CurrentUnit;
            if(previousUnit == newUnit)
                return;
            CurrentUnit = newUnit;

            // Build a function that converts values from previousUnit -> newUnit
            Func<float, float> converter;

            // Big distance
             converter = (previousUnit, newUnit) switch {
                (UnitSystem.Kilometers, UnitSystem.StatuteMiles) => v => v / mi_to_km, // km -> mi
                (UnitSystem.Kilometers, UnitSystem.NauticalMiles) => v => v / nm_to_km, // km -> nm
                (UnitSystem.StatuteMiles, UnitSystem.Kilometers) => v => v * mi_to_km, // mi -> km
                (UnitSystem.StatuteMiles, UnitSystem.NauticalMiles) => v => v / nm_to_mi, // mi -> nm
                (UnitSystem.NauticalMiles, UnitSystem.Kilometers) => v => v * nm_to_km, // nm -> km
                (UnitSystem.NauticalMiles, UnitSystem.StatuteMiles) => v => v * nm_to_mi, // nm -> mi
                _ => v => v // identity if same unit or unsupported combination
            };
            foreach(var subscriber in bigDistance_subscribers)
                subscriber(converter);

            // Small distance
            converter = (previousUnit, newUnit) switch {
                (UnitSystem.Kilometers, UnitSystem.StatuteMiles) => v => v * m_to_ft, // m -> ft
                (UnitSystem.Kilometers, UnitSystem.NauticalMiles) => v => v * m_to_ft, // m -> ft
                (UnitSystem.StatuteMiles, UnitSystem.Kilometers) => v => v / m_to_ft, // ft -> m
                (UnitSystem.StatuteMiles, UnitSystem.NauticalMiles) => v => v, // ft -> ft
                (UnitSystem.NauticalMiles, UnitSystem.Kilometers) => v => v / m_to_ft, // ft -> m
                (UnitSystem.NauticalMiles, UnitSystem.StatuteMiles) => v => v, // ft -> ft
                _ => v => v // identity if same unit or unsupported combination
            };
            foreach(var subscriber in smallDistance_subscribers)
                subscriber(converter);

            // Big speed
            converter = (previousUnit, newUnit) switch {
                (UnitSystem.Kilometers, UnitSystem.StatuteMiles) => v => v / mi_to_km, // kph -> mph
                (UnitSystem.Kilometers, UnitSystem.NauticalMiles) => v => v / nm_to_km, // kph -> knt
                (UnitSystem.StatuteMiles, UnitSystem.Kilometers) => v => v * mi_to_km, // mph -> kph
                (UnitSystem.StatuteMiles, UnitSystem.NauticalMiles) => v => v / nm_to_mi, // mph -> knt
                (UnitSystem.NauticalMiles, UnitSystem.Kilometers) => v => v * nm_to_km, // knt -> kph
                (UnitSystem.NauticalMiles, UnitSystem.StatuteMiles) => v => v * nm_to_mi, // knt -> mph
                _ => v => v // identity if same unit or unsupported combination
            };
            foreach(var subscriber in bigSpeed_subscribers)
                subscriber(converter);

            // Small speed
            converter = (previousUnit, newUnit) switch {
                (UnitSystem.Kilometers, UnitSystem.StatuteMiles) => v => v * mps_to_fpm, // mps -> fpm
                (UnitSystem.Kilometers, UnitSystem.NauticalMiles) => v => v * mps_to_fpm, // mps -> fpm
                (UnitSystem.StatuteMiles, UnitSystem.Kilometers) => v => v / mps_to_fpm, // fpm -> mps
                (UnitSystem.StatuteMiles, UnitSystem.NauticalMiles) => v => v, // fpm -> fpm
                (UnitSystem.NauticalMiles, UnitSystem.Kilometers) => v => v / mps_to_fpm, // fpm -> mps
                (UnitSystem.NauticalMiles, UnitSystem.StatuteMiles) => v => v, // fpm -> fpm
                _ => v => v // identity if same unit or unsupported combination
            };
            foreach(var subscriber in smallSpeed_subscribers)
                subscriber(converter);
        }
        internal class VariableUnitBigDistance {
            public float distance;
            public VariableUnitBigDistance() {
                // Subscribe: when the unit changes, apply the conversion
                bigDistance_subscribers.Add(convert => {
                    distance = convert(distance);
                });
            }
            public float to_SI() {
                return CurrentUnit switch {
                    UnitSystem.Kilometers => distance * km_to_m, // km -> m
                    UnitSystem.StatuteMiles => distance * mi_to_m, // mi -> m
                    UnitSystem.NauticalMiles => distance * nm_to_m, // nm -> m
                    _ => distance,
                };
            }
            public void add_SI(float delta_m) {
                distance += CurrentUnit switch {
                    UnitSystem.Kilometers => delta_m / km_to_m, // m -> km
                    UnitSystem.StatuteMiles => delta_m / mi_to_m, // m -> mi
                    UnitSystem.NauticalMiles => delta_m / nm_to_m, // m -> nm
                    _ => delta_m,
                };
            }
        }
        internal class VariableUnitSmallDistance {
            public float distance;

            public VariableUnitSmallDistance() {
                // Subscribe: when the unit changes, apply the conversion
                smallDistance_subscribers.Add(convert => {
                    distance = convert(distance);
                });
            }
            public float to_SI() {
                return CurrentUnit switch {
                    UnitSystem.Kilometers => distance, // m -> m
                    _ => distance / m_to_ft, // ft -> m
                };
            }
            public void add_SI(float delta_m) {
                distance += CurrentUnit switch {
                    UnitSystem.Kilometers => delta_m, // m -> m
                    _ => delta_m * m_to_ft, // m -> ft
                };
            }
        }
        internal class VariableUnitBigSpeed {
            public float speed;
            public VariableUnitBigSpeed() {
                // Subscribe: when the unit changes, apply the conversion
                bigSpeed_subscribers.Add(convert => {
                    speed = convert(speed);
                });
            }
            public float to_SI() {
                return CurrentUnit switch {
                    UnitSystem.Kilometers => speed / mps_to_kph, // kph -> mps
                    UnitSystem.StatuteMiles => speed / mps_to_mph, // mph -> mps
                    UnitSystem.NauticalMiles => speed / mps_to_knt, // knt -> mps
                    _ => speed * nm_to_km, // knt -> kph
                };
            }
            public void add_SI(float delta_mps) {
                speed += CurrentUnit switch {
                    UnitSystem.Kilometers => delta_mps * mps_to_kph, // mps -> kph
                    UnitSystem.StatuteMiles => delta_mps * mps_to_mph, // mps -> mph
                    UnitSystem.NauticalMiles => delta_mps * mps_to_knt, // mps -> knt
                    _ => delta_mps,
                };
            }
        }
        internal class VariableUnitSmallSpeed {
            public float speed;
            public VariableUnitSmallSpeed() {
                // Subscribe: when the unit changes, apply the conversion
                smallSpeed_subscribers.Add(convert => {
                    speed = convert(speed);
                });
            }
            public float to_SI() {
                return CurrentUnit switch {
                    UnitSystem.Kilometers => speed, // mps -> mps
                    _ => speed / mps_to_fpm, // fpm -> mps
                };
            }
            public void add_SI(float delta_mps) {
                speed += CurrentUnit switch {
                    UnitSystem.Kilometers => delta_mps, // mps -> mps
                    _ => delta_mps * mps_to_fpm, // mps -> fpm
                };
            }
        }
    }
}