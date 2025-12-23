namespace Shifts.API.ShiftsHandler.GenerateShifts;

public record GenerateShiftsCommand(
    Guid ManagerId,
    List<Guid> ShiftTemplateIds,
    DateTime? GenerateFromDate = null,
    int GenerateDays = 30
) : ICommand<GenerateShiftsResult>;


public record GenerateShiftsResult
{
    public int ShiftsCreatedCount { get; init; }
    public int ShiftsSkippedCount { get; init; }
    public List<SkipReason> SkipReasons { get; init; } = new();
    public List<Guid> CreatedShiftIds { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public DateTime GeneratedFrom { get; init; }
    public DateTime GeneratedTo { get; init; }
}

public record SkipReason
{
    public DateTime Date { get; init; }
    public Guid? LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string ScheduleName { get; init; } = string.Empty;
}
