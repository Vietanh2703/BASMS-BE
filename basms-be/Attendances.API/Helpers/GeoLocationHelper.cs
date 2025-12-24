namespace Attendances.API.Helpers;

public static class GeoLocationHelper
{
    private const double EarthRadiusKm = 6371.0;

    public static double CalculateDistanceInMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        var distanceKm = EarthRadiusKm * c;
        return distanceKm * 1000; 
    }
    
    public static bool IsWithinRange(double lat1, double lon1, double lat2, double lon2, double maxDistanceMeters)
    {
        var distance = CalculateDistanceInMeters(lat1, lon1, lat2, lon2);
        return distance <= maxDistanceMeters;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
