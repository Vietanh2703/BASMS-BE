namespace Contracts.API.Models;

[Table("contract_locations")]
public class ContractLocation
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid LocationId { get; set; }
    public int GuardsRequired { get; set; }
    public string CoverageType { get; set; } = string.Empty;
    public DateTime ServiceStartDate { get; set; }
    public DateTime? ServiceEndDate { get; set; }
    public bool IsPrimaryLocation { get; set; } = false;
    public int PriorityLevel { get; set; } = 1;
    public bool AutoGenerateShifts { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
