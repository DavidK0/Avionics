using Brutal.Numerics;

namespace Avionics {
    public class Airport {
        public string Ident { get; set; }
        public string Name { get; set; }
        public double longitude_deg { get; set; }
        public double Latitude_deg { get; set; }
        public List<Runway> Runways { get; set; }

    }

    public class Runway {
        // Loaded from JSON data
        public double Length_m { get; set; }
        public double Width_m { get; set; }
        public string Surface { get; set; }
        public string Le_Ident { get; set; }
        public double Le_Latitude_rad { get; set; }
        public double Le_Longitude_rad { get; set; }
        public double Le_Elevation_m { get; set; }
        public double Le_True_Heading_rad { get; set; }
        public string He_Ident { get; set; }
        public double He_Latitude_rad { get; set; }
        public double He_Longitude_rad { get; set; }
        public double He_Elevation_m { get; set; }
        public bool Use_LE { get; set; }
        public double glideSlopeRad { get; set; }
        public double3 GetGPS() {
            if(Use_LE) {
                return new double3(Le_Latitude_rad, Le_Longitude_rad, Le_Elevation_m);
            } else {
                return new double3(He_Latitude_rad, He_Longitude_rad, He_Elevation_m);
            }
        }
        public string GetDescription() {
            return $"{Le_Ident}/{He_Ident} Length: {(int)Length_m} m Surface: {Surface}";
        }
        public string GetIdent() {
            return Use_LE ? Le_Ident : He_Ident;
        }
        // Get true heading of currently selected runway
        public double GetTrueHeading() {
            if(Use_LE) {
                return Le_True_Heading_rad;
            } else {
                return (Le_True_Heading_rad + Math.PI) % (2.0 * Math.PI);
            }
        }
        public double GetCurrentVerticalAngle(double3 GPSPos, double radius) {
            // Aircraft position
            double aircraftLat_rad = GPSPos.X;   // latitude
            double aircraftLon_rad = GPSPos.Y;   // longitude
            double aircraftAlt_m = GPSPos.Z;   // altitude

            // Runway end currently in use
            double3 rw = GetGPS();  // Returns (lat, lon, elev) in radians / meters

            double rwLat_rad = rw.X;
            double rwLon_rad = rw.Y;
            double rwAlt_m = rw.Z;

            double3 aircraftPos = new double3(aircraftLat_rad, aircraftLon_rad, aircraftAlt_m);
            double3 runwayPos = new double3(rwLat_rad, rwLon_rad, rwAlt_m);

            double horizontal_m = Geomath.GetDistance(aircraftPos, runwayPos, radius);

            // Vertical difference: aircraft altitude above runway threshold
            double deltaAlt_m = aircraftAlt_m - rwAlt_m;

            // Vertical angle toward the runway end
            double angle_rad = Math.Atan2(deltaAlt_m, horizontal_m);

            return (float)angle_rad;
        }
        public double GetVerticalDeviation(double3 GPSPos, double radius) {
            return GetCurrentVerticalAngle(GPSPos, radius) - glideSlopeRad;
        }

        public double GetLateralDeviation(double3 GPSPos, double heading) {
            double bearing = GetBearing(GPSPos);

            double diff = (GetTrueHeading() - bearing) % (2 * Math.PI);

            if(diff > Math.PI)
                diff -= 2 * Math.PI;
            else if(diff < -Math.PI)
                diff += 2 * Math.PI;

            return diff;
        }

        public double GetLateralDeviation_dist(double3 GPSPos, double radius) {
            // Runway (great-circle) definition
            double3 runway_gps = GetGPS();
            double runway_lat_rad = runway_gps[0];
            double runway_long_rad = runway_gps[1];
            double runway_heading_rad = GetTrueHeading();    // θ12, radians from north

            // Point (aircraft) position
            double point_lat_rad = GPSPos[0];
            double point_long_rad = GPSPos[1];

            // Angular distance between runway point (1) and aircraft point (3)
            // GetDistance returns arc length, so divide by radius to get central angle d13
            double d13 = Geomath.GetDistance(runway_gps, GPSPos, radius) / radius;

            // Initial bearing from runway point (1) to aircraft point (3) -> θ13
            double theta13 = Geomath.GetBearing(runway_gps, GPSPos);   // radians

            // Track of the great circle from the runway -> θ12
            double theta12 = runway_heading_rad;               // radians

            // Cross-track angular distance
            double xTrackAngle = Math.Asin(Math.Sin(d13) * Math.Sin(theta13 - theta12));

            // Convert angular distance to linear distance
            double lateralOffset = xTrackAngle * radius;

            return lateralOffset;
        }
        public double GetVerticalDeviation_dist(double3 GPSPos, double radius) {
            double vertical_angle = GetCurrentVerticalAngle(GPSPos, radius);
            double base_dist = Math.Cos(vertical_angle) * Geomath.GetDistance(GPSPos, GetGPS(), radius);
            double correct_rise = Math.Tan(glideSlopeRad) * base_dist;
            double current_rise = GPSPos.Z;
            return current_rise - correct_rise;
        }

        public double GetBearing(double3 GPSPos) {
            return Geomath.GetBearing(GPSPos, GetGPS());
        }
    }
}
