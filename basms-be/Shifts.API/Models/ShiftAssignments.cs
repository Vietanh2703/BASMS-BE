namespace Shifts.API.Models;

[Table("shift_assignments")]
public class ShiftAssignments
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid ShiftId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid GuardId { get; set; }
    public string AssignmentType { get; set; } = "REGULAR";
    public Guid? ReplacedGuardId { get; set; }
    public string? ReplacementReason { get; set; }

    public bool IsReplacement { get; set; } = false;
    public string Status { get; set; } = "ASSIGNED";
    public DateTime AssignedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? DeclinedAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? DeclineReason { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? AttendanceRecordId { get; set; }
    public bool AttendanceSynced { get; set; } = false;
    public bool NotificationSent { get; set; } = false;
    public DateTime? NotificationSentAt { get; set; }
    public string? NotificationMethod { get; set; }
    public bool Reminder24HSent { get; set; } = false;
    public DateTime? Reminder24HSentAt { get; set; }
    public bool Reminder2HSent { get; set; } = false;
    public DateTime? Reminder2HSentAt { get; set; }
    public decimal? PunctualityScore { get; set; }
    public string? PerformanceNote { get; set; }
    public Guid? RatedBy { get; set; }
    public DateTime? RatedAt { get; set; }
    public string? AssignmentNotes { get; set; }
    public string? GuardNotes { get; set; }
    public Guid AssignedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    
    [Write(false)]
    [Computed]
    public virtual Shifts? Shift { get; set; }

    [Write(false)]
    [Computed]
    public virtual Teams? Team { get; set; }

    [Write(false)]
    [Computed]
    public virtual Guards? Guard { get; set; }

    [Write(false)]
    [Computed]
    public virtual Guards? ReplacedGuard { get; set; }
    
    [Write(false)]
    [Computed]
    public virtual Managers? AssignedByManager { get; set; }
    
    [Write(false)]
    [Computed]
    public virtual Managers? RatedByManager { get; set; }
}