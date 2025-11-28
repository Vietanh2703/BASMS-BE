namespace Contracts.API.Extensions;

/// <summary>
/// Extension methods cho DateTime operations
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Get current time in Vietnam timezone (UTC+7)
    /// </summary>
    public static DateTime GetVietnamTime()
    {
        return DateTime.UtcNow.AddHours(7);
    }

    /// <summary>
    /// Convert UTC time to Vietnam time (UTC+7)
    /// </summary>
    public static DateTime ToVietnamTime(this DateTime utcDateTime)
    {
        return utcDateTime.AddHours(7);
    }

    /// <summary>
    /// Convert Vietnam time to UTC
    /// </summary>
    public static DateTime ToUtcFromVietnam(this DateTime vietnamDateTime)
    {
        return vietnamDateTime.AddHours(-7);
    }
}
