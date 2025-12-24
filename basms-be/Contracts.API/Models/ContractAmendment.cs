namespace Contracts.API.Models;


[Table("contract_amendments")]
public class ContractAmendment
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public string AmendmentNumber { get; set; } = string.Empty;
    public string AmendmentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string? ChangesSummary { get; set; }
    public string Status { get; set; } = "draft";
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? DocumentUrl { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
