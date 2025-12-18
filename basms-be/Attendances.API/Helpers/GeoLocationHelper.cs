namespace Attendances.API.Helpers;

/// <summary>
/// Helper class for geolocation calculations
/// </summary>
public static class GeoLocationHelper
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// Calculate distance between two geographic coordinates using Haversine formula
    /// </summary>
    /// <param name="lat1">Latitude of first point</param>
    /// <param name="lon1">Longitude of first point</param>
    /// <param name="lat2">Latitude of second point</param>
    /// <param name="lon2">Longitude of second point</param>
    /// <returns>Distance in meters</returns>
    public static double CalculateDistanceInMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        var distanceKm = EarthRadiusKm * c;
        return distanceKm * 1000; // Convert to meters
    }

    /// <summary>
    /// Check if distance is within acceptable range
    /// </summary>
    /// <param name="lat1">Latitude of first point</param>
    /// <param name="lon1">Longitude of first point</param>
    /// <param name="lat2">Latitude of second point</param>
    /// <param name="lon2">Longitude of second point</param>
    /// <param name="maxDistanceMeters">Maximum acceptable distance in meters</param>
    /// <returns>True if within range, false otherwise</returns>
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
