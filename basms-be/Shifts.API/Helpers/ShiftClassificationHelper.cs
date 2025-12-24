namespace Shifts.API.Helpers;

public static class ShiftClassificationHelper
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); 
    public static string ClassifyShiftTimeSlot(DateTime shiftStartUtc)
    {
        var shiftStartVietnam = TimeZoneInfo.ConvertTimeFromUtc(shiftStartUtc, VietnamTimeZone);
        var hour = shiftStartVietnam.Hour;

        if (hour >= 6 && hour < 14)
            return "MORNING";
        
        if (hour >= 14 && hour < 22)
            return "AFTERNOON";
        
        return "EVENING";
    }


    public static string ClassifyShiftTimeSlotFromTimeSpan(TimeSpan startTime)
    {
        var hour = startTime.Hours;

        if (hour >= 6 && hour < 14)
            return "MORNING";

        if (hour >= 14 && hour < 22)
            return "AFTERNOON";

        return "EVENING";
    }
    
    public static bool AreConsecutiveSlots(string slot1, string slot2)
    {
        var consecutivePairs = new[]
        {
            ("EVENING", "MORNING"),     
            ("MORNING", "AFTERNOON"),   
            ("AFTERNOON", "EVENING")    
        };

        return consecutivePairs.Any(pair =>
            (pair.Item1 == slot1 && pair.Item2 == slot2) ||
            (pair.Item2 == slot1 && pair.Item1 == slot2));
    }
    
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
