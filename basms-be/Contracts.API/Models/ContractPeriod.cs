namespace Contracts.API.Models;

[Table("contract_periods")]
public class ContractPeriod
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public int PeriodNumber { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    public bool IsCurrentPeriod { get; set; } = false;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
