namespace Shifts.API.Models;

[Table("shift_issues")]
public class ShiftIssues
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid? ShiftId { get; set; }
    public Guid? GuardId { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime IssueDate { get; set; }
    public string? EvidenceFileUrl { get; set; }
    public int TotalShiftsAffected { get; set; } = 0;
    public int TotalGuardsAffected { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
