namespace Attendances.API.Helpers;

/// <summary>
/// Helper class for DateTime operations with Vietnam timezone (UTC+7)
/// </summary>
public static class DateTimeHelper
{
    private static readonly TimeZoneInfo _vietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); // UTC+7

    /// <summary>
    /// Get current time in Vietnam timezone (UTC+7)
    /// </summary>
    public static DateTime VietnamNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _vietnamTimeZone);

    /// <summary>
    /// Convert UTC time to Vietnam time
    /// </summary>
    public static DateTime ToVietnamTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _vietnamTimeZone);
    }

    /// <summary>
    /// Convert Vietnam time to UTC
    /// </summary>
    public static DateTime ToUtc(DateTime vietnamDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(vietnamDateTime, _vietnamTimeZone);
    }
}
