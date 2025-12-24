namespace Attendances.API.Models;

[Table("attendance_exceptions")]
public class AttendanceExceptions
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid? AttendanceRecordId { get; set; }
    public Guid ShiftAssignmentId { get; set; }
    public Guid GuardId { get; set; }
    public Guid ShiftId { get; set; }
    public string ExceptionType { get; set; } = string.Empty;
    public string Severity { get; set; } = "MEDIUM";
    public string Description { get; set; } = string.Empty;
    public bool AutoDetected { get; set; } = true;
    public DateTime DetectedAt { get; set; }
    public string Status { get; set; } = "OPEN";
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public string? ResolutionNotes { get; set; }
    public string? ResolutionAction { get; set; }
    public bool AutoResolvable { get; set; } = false;
    public string? SuggestedAction { get; set; }
    public string? NotificationTemplate { get; set; }
    public decimal? PenaltyAmount { get; set; }
    public decimal? PerformanceImpact { get; set; }
    public string? ImpactNotes { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string ApprovalStatus { get; set; } = "PENDING";
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

}
