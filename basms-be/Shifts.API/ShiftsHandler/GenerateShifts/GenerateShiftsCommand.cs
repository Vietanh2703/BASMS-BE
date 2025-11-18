namespace Shifts.API.ShiftsHandler.GenerateShifts;

/// <summary>
/// Command để tự động tạo ca làm từ nhiều ShiftTemplates
/// Generate shifts from multiple shift templates
/// </summary>
/// <param name="ManagerId">ID của Manager tạo ca (REQUIRED)</param>
/// <param name="ShiftTemplateIds">Danh sách ID của ShiftTemplates (REQUIRED)</param>
/// <param name="GenerateFromDate">Tạo ca từ ngày nào (default: hôm nay)</param>
/// <param name="GenerateDays">Tạo trước bao nhiêu ngày (default: 30)</param>
public record GenerateShiftsCommand(
    Guid ManagerId,
    List<Guid> ShiftTemplateIds,
    DateTime? GenerateFromDate = null,
    int GenerateDays = 30
) : ICommand<GenerateShiftsResult>;

/// <summary>
/// Kết quả generate shifts
/// </summary>
public record GenerateShiftsResult
{
    /// <summary>
    /// Tổng số ca đã tạo thành công
    /// </summary>
    public int ShiftsCreatedCount { get; init; }

    /// <summary>
    /// Số ca bị bỏ qua (do lễ, đóng cửa, exception...)
    /// </summary>
    public int ShiftsSkippedCount { get; init; }

    /// <summary>
    /// Danh sách lý do bỏ qua
    /// </summary>
    public List<SkipReason> SkipReasons { get; init; } = new();

    /// <summary>
    /// Danh sách ID các ca đã tạo
    /// </summary>
    public List<Guid> CreatedShiftIds { get; init; } = new();

    /// <summary>
    /// Thông báo lỗi nếu có
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Ngày bắt đầu generate
    /// </summary>
    public DateTime GeneratedFrom { get; init; }

    /// <summary>
    /// Ngày kết thúc generate
    /// </summary>
    public DateTime GeneratedTo { get; init; }
}

/// <summary>
/// Lý do bỏ qua tạo ca
/// </summary>
public record SkipReason
{
    public DateTime Date { get; init; }
    public Guid? LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string ScheduleName { get; init; } = string.Empty;
}
