using Brutal.Numerics;
using KSA;

namespace Avionics {
    internal class NavigationSystem {
        internal double3 targetGPS;
        internal float targetBearing_rad;
        internal double targetDistance_m;
        internal float targetSlope_rad;
        internal float lateralDeviation;
        internal float verticalDeviation;
        internal float2 targetDeviation;
        public NavigationSystem() { }
        public void Update(double3 vehicleGPS, FlightManagementSystem.ApproachProcedure activeApproach, float planetRadius) {
            targetGPS = activeApproach.Runway.GetGPS();
            targetBearing_rad = (float)Geomath.GetBearing(vehicleGPS, targetGPS);
            targetDistance_m = (float)Geomath.GetDistance(vehicleGPS, targetGPS, planetRadius);
            targetSlope_rad = (float)activeApproach.Runway.GetCurrentVerticalAngle(vehicleGPS, planetRadius);
            lateralDeviation = (float)activeApproach.Runway.GetLateralDeviation(vehicleGPS, planetRadius);
            verticalDeviation = (float)activeApproach.Runway.GetVerticalDeviation(vehicleGPS, planetRadius);
            targetDeviation = new float2(lateralDeviation, verticalDeviation);
        }
    }

}
