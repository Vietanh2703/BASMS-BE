namespace Shifts.API.Helpers;

/// <summary>
/// Helper để phân loại ca trực dựa vào ShiftStart
/// Sử dụng giờ Việt Nam (UTC+7)
/// </summary>
public static class ShiftClassificationHelper
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); // UTC+7

    /// <summary>
    /// Phân loại ca dựa vào ShiftStart (UTC) → Chuyển sang giờ Việt Nam
    ///
    /// CLASSIFICATION RULES (theo giờ Việt Nam):
    /// - MORNING:   06:00 - 13:59
    /// - AFTERNOON: 14:00 - 21:59
    /// - EVENING:   22:00 - 05:59 (qua đêm)
    /// </summary>
    public static string ClassifyShiftTimeSlot(DateTime shiftStartUtc)
    {
        // Convert UTC → Vietnam Time
        var shiftStartVietnam = TimeZoneInfo.ConvertTimeFromUtc(shiftStartUtc, VietnamTimeZone);
        var hour = shiftStartVietnam.Hour;

        // MORNING: 6h - 13h59
        if (hour >= 6 && hour < 14)
            return "MORNING";

        // AFTERNOON: 14h - 21h59
        if (hour >= 14 && hour < 22)
            return "AFTERNOON";

        // EVENING: 22h - 5h59 (qua đêm)
        return "EVENING";
    }

    /// <summary>
    /// Phân loại ca dựa vào TimeSpan (dùng cho ad-hoc shifts)
    /// </summary>
    public static string ClassifyShiftTimeSlotFromTimeSpan(TimeSpan startTime)
    {
        var hour = startTime.Hours;

        if (hour >= 6 && hour < 14)
            return "MORNING";

        if (hour >= 14 && hour < 22)
            return "AFTERNOON";

        return "EVENING";
    }

    /// <summary>
    /// Kiểm tra 2 ca có kế tiếp nhau không
    /// </summary>
    public static bool AreConsecutiveSlots(string slot1, string slot2)
    {
        var consecutivePairs = new[]
        {
            ("EVENING", "MORNING"),     // Ca đêm (22h-6h) kế tiếp ca sáng (6h-14h)
            ("MORNING", "AFTERNOON"),   // Ca sáng (6h-14h) kế tiếp ca chiều (14h-22h)
            ("AFTERNOON", "EVENING")    // Ca chiều (14h-22h) kế tiếp ca tối (22h-6h)
        };

        return consecutivePairs.Any(pair =>
            (pair.Item1 == slot1 && pair.Item2 == slot2) ||
            (pair.Item2 == slot1 && pair.Item1 == slot2));
    }

    /// <summary>
    /// Lấy tên ca bằng tiếng Việt
    /// </summary>
    public static string GetVietnameseSlotName(string timeSlot)
    {
        return timeSlot switch
        {
            "MORNING" => "Ca Sáng (6h-14h)",
            "AFTERNOON" => "Ca Chiều (14h-22h)",
            "EVENING" => "Ca Tối/Đêm (22h-6h)",
            _ => timeSlot
        };
    }
}
