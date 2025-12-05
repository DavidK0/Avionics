using Brutal.Numerics;
using KSA;

namespace Avionics {
    internal class Geomath {
        internal const float Rad2Deg = 180.0f / (float)Math.PI;
        internal const float Deg2Rad = (float)Math.PI / 180.0f;

        // Return GPS position as (latitude [radians], longitude [radians], altitude [meters])
        public static double3 GetGPSPosition(Vehicle vehicle) {
            if(vehicle == null) return new double3(0.0, 0.0, 0.0);

            Astronomical parent = vehicle.Orbit.Parent;
            if(vehicle.Orbit.Parent is not Celestial) return new double3(0.0, 0.0, 0.0);
            Celestial celestial = (Celestial)vehicle.Orbit.Parent;
            double3 surface_position = celestial.GetCci2Ccf() * vehicle.GetPositionCci();

            double x = surface_position[0];
            double y = surface_position[1];
            double z = surface_position[2];

            double r = Math.Sqrt(x * x + y * y + z * z);

            double latitude = Math.Asin(z / r);     // [-pi/2, +pi/2]
            double longitude = Math.Atan2(y, x);     // [-pi, +pi]
            double altitude = r - parent.MeanRadius;

            return new double3(latitude, longitude, altitude);
        }
        public static string GetGPSPositionString(double3 GPSPos) {
            // Convert to degrees
            double latitude_deg = GPSPos[0] * (180.0 / Math.PI);
            double longitude_deg = GPSPos[1] * (180.0 / Math.PI);

            // Convert to string and append N/S, E/W
            string lat_hemisphere = latitude_deg >= 0 ? "N" : "S";
            string lon_hemisphere = longitude_deg >= 0 ? "E" : "W";
            string lat_str = $"{Math.Abs(latitude_deg):F4}° {lat_hemisphere}";
            string lon_str = $"{Math.Abs(longitude_deg):F4}° {lon_hemisphere}";

            return $"{lat_str}, {lon_str}";
        }
        public static double GetBearing(double3 GPSPos1, double3 GPSPos2) {
            double lat1 = GPSPos1[0];
            double lon1 = GPSPos1[1];

            double lat2 = GPSPos2[0];
            double lon2 = GPSPos2[1];

            double dLon = lon2 - lon1;

            // Great-circle initial bearing
            double y = Math.Sin(dLon) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Sin(lat2) -
                       Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

            double bearing = Math.Atan2(y, x);   // range [-π, π]

            // Normalize to [0, 2π)
            if(bearing < 0)
                bearing += 2.0 * Math.PI;

            return bearing;
        }

        public static string GetBearingString(double bearing_rad) {
            // Convert to degrees
            double bearing_deg = bearing_rad * (180.0f / Math.PI);
            return UnitControler.RadToString((float)bearing_rad);
        }

        public static double GetDistance(double3 pos1, double3 pos2, double radius) {
            double dLat = pos2[0] - pos1[0];
            double dLon = pos2[1] - pos1[1];

            double sinLat = Math.Sin(dLat * 0.5);
            double sinLon = Math.Sin(dLon * 0.5);

            double a =
                sinLat * sinLat +
                Math.Cos(pos1[0]) * Math.Cos(pos2[0]) *
                sinLon * sinLon;

            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

            return radius * c;   // arc length
        }

        public static Airport GetNearestAirport(double3 GPSPos, List<Airport> airports) {
            double best_dist = double.MaxValue;
            Airport best_airport = null;
            foreach(Airport airport in airports) {
                if(airport.Runways.Count > 0) {
                    double airport_lat_rad = Deg2Rad * airport.Latitude_deg;
                    double airport_lon_rad = Deg2Rad * airport.longitude_deg;
                    double3 airportGPS = new double3(airport_lat_rad, airport_lon_rad, 0.0);
                    double distance = GetDistance(GPSPos, airportGPS, 1.0); // actual radius does not matter for ranking distances
                    if(distance < best_dist) {
                        best_dist = distance;
                        best_airport = airport;
                    }
                }
            }
            return best_airport;
        }


        public static double GetHeading(Vehicle vehicle) {

            return GetSurfaceAttitude(vehicle).Z;
        }
        public static double GetHeadingDeg(double heading) {
            double deg = (heading + Math.PI / 2.0) * (180.0 / Math.PI);
            deg %= 360.0;
            if(deg < 0) deg += 360.0;
            return deg;
        }
        public static string GetHeadingString(double heading, int decimals = 2) {
            string format = "F" + decimals;
            return $"{GetHeadingDeg(heading).ToString(format)}°";
        }
        public static double3 GetSurfaceAttitude(Vehicle vehicle) {
            if (vehicle == null) return new double3(0.0, 0.0, 0.0);

            double3 Body2Cci = vehicle.GetBody2Cci().ToRollYawPitchRadians();

            doubleQuat enuBody2Cci = VehicleReferenceFrameEx.GetEnuBody2Cci(vehicle.GetPositionCci()) ?? doubleQuat.Identity;
            doubleQuat attitudeQuat = doubleQuat.Concatenate(vehicle.GetBody2Cci(), enuBody2Cci.Inverse());
            double3 rpy = attitudeQuat.ToRollPitchYawRadians();

            return rpy;
        }
    }
}