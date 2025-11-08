namespace BuildingBlocks.Messaging.Events;

// ================================================================
// REQUEST/RESPONSE: LẤY SHIFT SCHEDULES TỪ CONTRACT
// ================================================================

/// <summary>
/// Request lấy shift schedules từ Contracts.API
/// </summary>
public record GetContractShiftSchedulesRequest
{
    public Guid ContractId { get; init; }
}

/// <summary>
/// Response chứa shift schedules và locations
/// </summary>
public record GetContractShiftSchedulesResponse
{
    public Guid ContractId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public bool WorkOnPublicHolidays { get; init; }
    public List<ShiftScheduleInfo> Schedules { get; init; } = new();
    public List<LocationInfo> Locations { get; init; } = new();
}

/// <summary>
/// Thông tin shift schedule
/// </summary>
public record ShiftScheduleInfo
{
    public Guid ScheduleId { get; init; }
    public string ScheduleName { get; init; } = string.Empty;
    public string ScheduleType { get; init; } = string.Empty;
    public Guid? LocationId { get; init; }

    public TimeSpan ShiftStartTime { get; init; }
    public TimeSpan ShiftEndTime { get; init; }
    public bool CrossesMidnight { get; init; }
    public int BreakMinutes { get; init; }
    public int GuardsPerShift { get; init; }

    public bool AppliesMonday { get; init; }
    public bool AppliesTuesday { get; init; }
    public bool AppliesWednesday { get; init; }
    public bool AppliesThursday { get; init; }
    public bool AppliesFriday { get; init; }
    public bool AppliesSaturday { get; init; }
    public bool AppliesSunday { get; init; }

    public bool AppliesOnPublicHolidays { get; init; }
    public bool AppliesOnWeekends { get; init; }
    public bool SkipWhenLocationClosed { get; init; }

    public bool RequiresArmedGuard { get; init; }
    public bool RequiresSupervisor { get; init; }

    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
}

/// <summary>
/// Thông tin location
/// </summary>
public record LocationInfo
{
    public Guid LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public string LocationCode { get; init; } = string.Empty;
}

// ================================================================
// REQUEST/RESPONSE: KIỂM TRA NGÀY LỄ
// ================================================================

/// <summary>
/// Request kiểm tra ngày lễ
/// </summary>
public record CheckPublicHolidayRequest
{
    public DateTime Date { get; init; }
}

/// <summary>
/// Response ngày lễ
/// </summary>
public record CheckPublicHolidayResponse
{
    public bool IsHoliday { get; init; }
    public string? HolidayName { get; init; }
    public string? HolidayCategory { get; init; }
    public bool IsTetPeriod { get; init; }
}

// ================================================================
// REQUEST/RESPONSE: KIỂM TRA LOCATION ĐÓNG CỬA
// ================================================================

/// <summary>
/// Request kiểm tra location đóng cửa
/// </summary>
public record CheckLocationClosedRequest
{
    public Guid LocationId { get; init; }
    public DateTime Date { get; init; }
}

/// <summary>
/// Response location đóng cửa
/// </summary>
public record CheckLocationClosedResponse
{
    public bool IsClosed { get; init; }
    public string? Reason { get; init; }
    public string? DayType { get; init; }
}
