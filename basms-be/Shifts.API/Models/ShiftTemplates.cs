using Dapper.Contrib.Extensions;

namespace Shifts.API.Models;

[Table("shift_templates")]
public class ShiftTemplates
{
    [ExplicitKey]
    public Guid Id { get; set; }
    
    public Guid? ManagerId { get; set; }
    
    public Guid? ContractId { get; set; }
    public Guid? TeamId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal DurationHours { get; set; }
    public int BreakDurationMinutes { get; set; } = 60;
    public int PaidBreakMinutes { get; set; } = 0;
    public int UnpaidBreakMinutes { get; set; } = 60;
    public bool IsNightShift { get; set; } = false;
    public bool IsOvernight { get; set; } = false;
    public bool CrossesMidnight { get; set; } = false;
    public bool AppliesMonday { get; set; } = false;
    public bool AppliesTuesday { get; set; } = false;
    public bool AppliesWednesday { get; set; } = false;
    public bool AppliesThursday { get; set; } = false;
    public bool AppliesFriday { get; set; } = false;
    public bool AppliesSaturday { get; set; } = false;
    public bool AppliesSunday { get; set; } = false;
    public int MinGuardsRequired { get; set; } = 1;
    public int? MaxGuardsAllowed { get; set; }
    public int? OptimalGuards { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAddress { get; set; }
    public decimal? LocationLatitude { get; set; }
    public decimal? LocationLongitude { get; set; }
    public string Status { get; set; } = "await_create_shift";

    public bool IsActive { get; set; } = true;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    [Write(false)]
    [Computed]
    public virtual ICollection<Shifts> Shifts { get; set; } = new List<Shifts>();
    
    [Write(false)]
    [Computed]
    public virtual ICollection<RecurringShiftPatterns> RecurringPatterns { get; set; } = new List<RecurringShiftPatterns>();
}