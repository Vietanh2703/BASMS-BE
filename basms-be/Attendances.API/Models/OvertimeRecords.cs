namespace Attendances.API.Models;

[Table("overtime_records")]
public class OvertimeRecords
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid AttendanceRecordId { get; set; }
    public Guid GuardId { get; set; }
    public Guid ShiftId { get; set; }
    public string OvertimeType { get; set; } = "REGULAR_OT";
    public DateTime PlannedOvertimeStart { get; set; }
    public DateTime PlannedOvertimeEnd { get; set; }
    public DateTime? ActualOvertimeStart { get; set; }
    public DateTime? ActualOvertimeEnd { get; set; }
    public int PlannedOvertimeMinutes { get; set; }
    public int? ActualOvertimeMinutes { get; set; }
    public decimal? ActualOvertimeHours { get; set; }
    public decimal OvertimeRate { get; set; } = 1.5m;
    public decimal? BaseHourlyRate { get; set; }
    public string Status { get; set; } = "PENDING";
    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ManagerNotes { get; set; }
    public bool IsMandatory { get; set; } = false;
    public bool IsEmergency { get; set; } = false;
    public bool IsPaid { get; set; } = false;
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

}
