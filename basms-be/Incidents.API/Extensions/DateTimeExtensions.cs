namespace Incidents.API.Extensions;

public static class DateTimeExtensions
{
    public static DateTime GetVietnamTime()
    {
        return DateTime.UtcNow.AddHours(7);
    }

    public static DateTime ToVietnamTime(this DateTime utcDateTime)
    {
        return utcDateTime.AddHours(7);
    }

    public static DateTime ToUtcFromVietnam(this DateTime vietnamDateTime)
    {
        return vietnamDateTime.AddHours(-7);
    }
}
