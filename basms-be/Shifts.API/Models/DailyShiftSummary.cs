namespace Shifts.API.Models;

[Table("daily_shift_summary")]
public class DailyShiftSummary
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public DateTime SummaryDate { get; set; }
    public int SummaryDay { get; set; }
    public int SummaryMonth { get; set; }
    public int SummaryYear { get; set; }
    public int SummaryQuarter { get; set; }
    public int SummaryWeek { get; set; }
    public int DayOfWeek { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? TeamId { get; set; }
    public bool IsWeekend { get; set; } = false;
    public bool IsPublicHoliday { get; set; } = false;
    public bool IsTetHoliday { get; set; } = false;
    public string? HolidayName { get; set; }
    public int TotalShiftsScheduled { get; set; } = 0;
    public int TotalShiftsInProgress { get; set; } = 0;
    public int TotalShiftsCompleted { get; set; } = 0;
    public int TotalShiftsCancelled { get; set; } = 0;
    public int TotalGuardsRequired { get; set; } = 0;
    public int TotalGuardsAssigned { get; set; } = 0;
    public int TotalGuardsConfirmed { get; set; } = 0;
    public int TotalGuardsCheckedIn { get; set; } = 0;
    public int TotalGuardsCompleted { get; set; } = 0;
    public int TotalGuardsNoShow { get; set; } = 0;
    public int TotalGuardsAbsent { get; set; } = 0;
    public decimal TotalScheduledHours { get; set; } = 0;
    public decimal TotalRegularHours { get; set; } = 0;
    public decimal TotalOvertimeHours { get; set; } = 0;
    public decimal TotalNightHours { get; set; } = 0;
    public decimal? AverageStaffingRate { get; set; }
    public DateTime? LastCalculatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    [Write(false)]
    [Computed]
    public virtual Teams? Team { get; set; }
}