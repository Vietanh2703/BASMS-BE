namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// BATCH Request kiểm tra nhiều ngày lễ cùng lúc
/// Tối ưu hóa: Thay vì gọi 30 lần (1 lần/ngày), chỉ gọi 1 lần cho cả tháng
/// Performance: 30x faster!
/// </summary>
public record BatchCheckPublicHolidaysRequest
{
    /// <summary>
    /// Danh sách ngày cần kiểm tra (ví dụ: 30 ngày)
    /// </summary>
    public List<DateTime> Dates { get; init; } = new();
}

/// <summary>
/// BATCH Response chứa tất cả ngày lễ trong khoảng
/// </summary>
public record BatchCheckPublicHolidaysResponse
{
    /// <summary>
    /// Dictionary: Date -> Holiday Info
    /// Key: Ngày (yyyy-MM-dd)
    /// Value: Thông tin ngày lễ (null nếu không phải ngày lễ)
    /// </summary>
    public Dictionary<DateTime, HolidayInfo?> Holidays { get; init; } = new();
}

/// <summary>
/// Thông tin ngày lễ
/// </summary>
public record HolidayInfo
{
    public string HolidayName { get; init; } = string.Empty;
    public string? HolidayNameEn { get; init; }
    public string HolidayCategory { get; init; } = string.Empty;
    public bool IsTetPeriod { get; init; }
    public bool IsTetHoliday { get; init; }
    public int? TetDayNumber { get; init; }
}

// ================================================================
// REQUEST/RESPONSE: LẤY CONTRACT INFO
// ================================================================

/// <summary>
/// Request lấy thông tin contract từ Contracts.API
/// </summary>
public record GetContractRequest
{
    public Guid ContractId { get; init; }
}

/// <summary>
/// Response chứa contract info
/// </summary>
public record GetContractResponse
{
    public bool Success { get; init; }
    public ContractData? Contract { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Contract data transfer object
/// </summary>
public record ContractData
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractTitle { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool AutoGenerateShifts { get; init; }
    public int GenerateShiftsAdvanceDays { get; init; }
    public Guid? CreatedBy { get; init; }
}
