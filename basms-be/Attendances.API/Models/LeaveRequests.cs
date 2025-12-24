namespace Attendances.API.Models;

[Table("leave_requests")]
public class LeaveRequests
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid GuardId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? HandoverToGuardId { get; set; }

    /// <summary>
    /// Loại nghỉ:
    /// ANNUAL_LEAVE = Nghỉ phép năm (có lương)
    /// SICK_LEAVE = Nghỉ ốm (có lương theo quy định)
    /// UNPAID_LEAVE = Nghỉ không lương
    /// MATERNITY_LEAVE = Nghỉ thai sản
    /// PATERNITY_LEAVE = Nghỉ chế độ (cha)
    /// BEREAVEMENT_LEAVE = Nghỉ tang
    /// MARRIAGE_LEAVE = Nghỉ cưới
    /// EMERGENCY_LEAVE = Nghỉ khẩn cấp
    /// OTHER = Khác
    /// </summary>
    public string LeaveType { get; set; } = string.Empty;
    public string LeaveScale { get; set; } = "FULL_DAY";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalDays { get; set; }
    public decimal TotalWorkDays { get; set; }
    public decimal? TotalHours { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? SupportingDocumentUrl { get; set; }
    public string? Notes { get; set; }
    public string? ManagerNotes { get; set; }
    public bool HasHandover { get; set; } = false;
    public Guid? ReplacementGuardId { get; set; }
    public string? HandoverNotes { get; set; }
    public string? ContactDuringLeave { get; set; }
    public string? EmergencyContact { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTime SubmittedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public bool IsPaid { get; set; } = true;
    public decimal PaymentPercentage { get; set; } = 100m;
    public bool DeductsFromAnnualLeave { get; set; } = false;
    public decimal? AnnualLeaveDaysDeducted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public int Version { get; set; } = 1;

}
