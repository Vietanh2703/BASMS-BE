namespace Contracts.API.Models;

[Table("shift_generation_log")]
public class ShiftGenerationLog
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid? ContractShiftScheduleId { get; set; }
    public DateTime GenerationDate { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int ShiftsCreatedCount { get; set; } = 0;
    public int ShiftsSkippedCount { get; set; } = 0;
    public string? SkipReasons { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? GeneratedByJob { get; set; }
}
