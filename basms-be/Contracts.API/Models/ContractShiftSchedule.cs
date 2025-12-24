namespace Contracts.API.Models;

[Table("contract_shift_schedules")]
public class ContractShiftSchedule
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid? LocationId { get; set; }
    public string ScheduleName { get; set; } = string.Empty;
    public string ScheduleType { get; set; } = "regular";
    public TimeSpan ShiftStartTime { get; set; }
    public TimeSpan ShiftEndTime { get; set; }
    public bool CrossesMidnight { get; set; } = false;
    public decimal DurationHours { get; set; }
    public int BreakMinutes { get; set; } = 0;
    public int GuardsPerShift { get; set; }
    public string RecurrenceType { get; set; } = "weekly";
    public bool AppliesMonday { get; set; } = false;
    public bool AppliesTuesday { get; set; } = false;
    public bool AppliesWednesday { get; set; } = false;
    public bool AppliesThursday { get; set; } = false;
    public bool AppliesFriday { get; set; } = false;
    public bool AppliesSaturday { get; set; } = false;
    public bool AppliesSunday { get; set; } = false;
    public string? MonthlyDates { get; set; }
    public bool AppliesOnPublicHolidays { get; set; } = true;
    public bool AppliesOnCustomerHolidays { get; set; } = true;
    public bool AppliesOnWeekends { get; set; } = true;
    public bool SkipWhenLocationClosed { get; set; } = false;
    public bool RequiresArmedGuard { get; set; } = false;
    public bool RequiresSupervisor { get; set; } = false;
    public int MinimumExperienceMonths { get; set; } = 0;
    public string? RequiredCertifications { get; set; }
    public bool AutoGenerateEnabled { get; set; } = true;
    public int GenerateAdvanceDays { get; set; } = 30;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
