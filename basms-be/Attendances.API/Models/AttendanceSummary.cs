using Dapper.Contrib.Extensions;

namespace Attendances.API.Models;


[Table("attendance_summary")]
public class AttendanceSummary
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid GuardId { get; set; }
    public Guid? TeamId { get; set; }
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    public string PeriodType { get; set; } = "MONTHLY";
    public int? SummaryMonth { get; set; }
    public int? SummaryYear { get; set; }
    public int? SummaryQuarter { get; set; }
    public int? SummaryWeek { get; set; }
    public int TotalShiftsAssigned { get; set; } = 0;
    public int TotalShiftsAttended { get; set; } = 0;
    public int TotalShiftsCompleted { get; set; } = 0;
    public int TotalAbsences { get; set; } = 0;
    public int TotalLateCount { get; set; } = 0;
    public int TotalEarlyLeaveCount { get; set; } = 0;
    public decimal TotalScheduledHours { get; set; } = 0;
    public decimal TotalActualHours { get; set; } = 0;
    public decimal TotalOvertimeHours { get; set; } = 0;
    public decimal TotalDayHours { get; set; } = 0;
    public decimal TotalNightHours { get; set; } = 0;
    public int TotalApprovedLeaves { get; set; } = 0;
    public decimal TotalPaidLeaveDays { get; set; } = 0;
    public decimal TotalUnpaidLeaveDays { get; set; } = 0;
    public decimal TotalSickLeaveDays { get; set; } = 0;
    public int TotalLateMinutes { get; set; } = 0;
    public int TotalEarlyLeaveMinutes { get; set; } = 0;
    public decimal? PunctualityScore { get; set; }
    public decimal? CompletionRate { get; set; }
    public int TotalExceptions { get; set; } = 0;
    public int ResolvedExceptions { get; set; } = 0;
    public int PendingExceptions { get; set; } = 0;
    public decimal? PerformanceScore { get; set; }
    public string? PerformanceNotes { get; set; }
    public bool IsReviewed { get; set; } = false;
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CalculatedAt { get; set; }
    public int CalculationVersion { get; set; } = 1;
    public bool IsAutoCalculated { get; set; } = true;
    public bool IsFinalized { get; set; } = false;
    public DateTime? FinalizedAt { get; set; }
    public Guid? FinalizedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

}
