namespace Shifts.API.ShiftsHandler.AssignTeamToShift;

/// <summary>
/// Command để assign team vào shifts theo date range và time slot
/// </summary>
public record AssignTeamToShiftCommand(
    Guid TeamId,                    // Team được assign
    DateTime StartDate,             // Ngày bắt đầu
    DateTime EndDate,               // Ngày kết thúc (multi-day assignment)
    string ShiftTimeSlot,           // MORNING | AFTERNOON | EVENING
    Guid LocationId,                // Địa điểm
    Guid? ContractId,               // Hợp đồng (optional)
    string AssignmentType,          // REGULAR | OVERTIME | MANDATORY
    string? AssignmentNotes,        // Ghi chú
    Guid AssignedBy                 // Manager thực hiện
) : ICommand<AssignTeamToShiftResult>;

/// <summary>
/// Result chứa thông tin assignments đã tạo
/// </summary>
public record AssignTeamToShiftResult
{
    public bool Success { get; init; }
    public int TotalDaysProcessed { get; init; }
    public int TotalShiftsAssigned { get; init; }
    public int TotalGuardsAssigned { get; init; }
    public List<DailyAssignmentSummary> DailySummaries { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public List<string> Errors { get; init; } = new();
}

/// <summary>
/// Tổng kết assignment cho từng ngày
/// </summary>
public record DailyAssignmentSummary
{
    public DateTime Date { get; init; }
    public Guid ShiftId { get; init; }
    public string ShiftCode { get; init; } = string.Empty;
    public string ShiftTimeSlot { get; init; } = string.Empty;
    public DateTime ShiftStart { get; init; }
    public DateTime ShiftEnd { get; init; }
    public int GuardsAssigned { get; init; }
    public List<string> GuardNames { get; init; } = new();
    public List<string> SkippedGuards { get; init; } = new();
}
