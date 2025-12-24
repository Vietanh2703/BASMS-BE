namespace Contracts.API.Models;

[Table("contract_shift_exceptions")]
public class ContractShiftException
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid ContractShiftScheduleId { get; set; }
    public DateTime ExceptionDate { get; set; }
    public string ExceptionType { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public TimeSpan? ModifiedStartTime { get; set; }
    public TimeSpan? ModifiedEndTime { get; set; }
    public int? ModifiedGuardsCount { get; set; }
    public string? SpecialInstructions { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
