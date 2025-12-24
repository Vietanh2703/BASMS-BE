namespace Shifts.API.Models;

[Table("shifts")]
public class Shifts
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid? ShiftTemplateId { get; set; }
    public Guid LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAddress { get; set; }
    public decimal? LocationLatitude { get; set; }
    public decimal? LocationLongitude { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? ManagerId { get; set; }
    public DateTime ShiftDate { get; set; }
    public int ShiftDay { get; set; }
    public int ShiftMonth { get; set; }
    public int ShiftYear { get; set; }
    public int ShiftQuarter { get; set; }
    public int ShiftWeek { get; set; }
    public int DayOfWeek { get; set; }
    public DateTime? ShiftEndDate { get; set; }
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public int TotalDurationMinutes { get; set; }
    public int WorkDurationMinutes { get; set; }
    public decimal WorkDurationHours { get; set; }
    public int BreakDurationMinutes { get; set; } = 60;
    public int PaidBreakMinutes { get; set; } = 0;
    public int UnpaidBreakMinutes { get; set; } = 60;
    public string ShiftType { get; set; } = "REGULAR";
    public bool IsRegularWeekday { get; set; } = true;
    public bool IsSaturday { get; set; } = false;
    public bool IsSunday { get; set; } = false;
    public bool IsPublicHoliday { get; set; } = false;
    public bool IsTetHoliday { get; set; } = false;
    public bool IsNightShift { get; set; } = false;
    public decimal NightHours { get; set; } = 0;
    public decimal DayHours { get; set; } = 0;
    public int RequiredGuards { get; set; } = 1;
    public int AssignedGuardsCount { get; set; } = 0;
    public int ConfirmedGuardsCount { get; set; } = 0;
    public int CheckedInGuardsCount { get; set; } = 0;
    public int CompletedGuardsCount { get; set; } = 0;
    public bool IsFullyStaffed { get; set; } = false;
    public bool IsUnderstaffed { get; set; } = false;
    public bool IsOverstaffed { get; set; } = false;
    public decimal? StaffingPercentage { get; set; }
    public string Status { get; set; } = "DRAFT";
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public bool IsMandatory { get; set; } = false;
    public bool IsCritical { get; set; } = false;
    public bool IsTrainingShift { get; set; } = false;
    public bool RequiresArmedGuard { get; set; } = false;
    public bool RequiresApproval { get; set; } = true;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string ApprovalStatus { get; set; } = "PENDING";
    public string? RejectionReason { get; set; }
    public string? Description { get; set; }
    public string? SpecialInstructions { get; set; }
    public string? EquipmentNeeded { get; set; }
    public string? EmergencyContacts { get; set; }
    public string? SiteAccessInfo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public int Version { get; set; } = 1;
    
    [Write(false)]
    [Computed]
    public virtual ShiftTemplates? ShiftTemplate { get; set; }

    [Write(false)]
    [Computed]
    public virtual Managers? Approver { get; set; }
    
    [Write(false)]
    [Computed]
    public virtual Managers? Creator { get; set; }

    [Write(false)]
    [Computed]
    public virtual ICollection<ShiftAssignments> Assignments { get; set; } = new List<ShiftAssignments>();

    [Write(false)]
    [Computed]
    public virtual ICollection<ShiftIssues> Issues { get; set; } = new List<ShiftIssues>();
}