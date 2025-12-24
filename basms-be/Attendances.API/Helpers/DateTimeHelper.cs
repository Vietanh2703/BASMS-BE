namespace Attendances.API.Helpers;


public static class DateTimeHelper
{
    private static readonly TimeZoneInfo _vietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); 

    public static DateTime VietnamNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _vietnamTimeZone);

    public static DateTime ToVietnamTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _vietnamTimeZone);
    }
    
    public static DateTime ToUtc(DateTime vietnamDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(vietnamDateTime, _vietnamTimeZone);
    }
}
