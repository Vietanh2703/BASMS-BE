namespace Shifts.API.Models;

[Table("recurring_shift_patterns")]
public class RecurringShiftPatterns
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid ShiftTemplateId { get; set; }
    public Guid LocationId { get; set; }
    public Guid? TeamId { get; set; }
    public string PatternName { get; set; } = string.Empty;
    public string RecurrenceType { get; set; } = string.Empty;
    public bool MondayEnabled { get; set; } = false;
    public bool TuesdayEnabled { get; set; } = false;
    public bool WednesdayEnabled { get; set; } = false;
    public bool ThursdayEnabled { get; set; } = false;
    public bool FridayEnabled { get; set; } = false;
    public bool SaturdayEnabled { get; set; } = false;
    public bool SundayEnabled { get; set; } = false;
    public int? MonthlyDayOfMonth { get; set; }
    public DateTime EffectiveStartDate { get; set; }
    public DateTime? EffectiveEndDate { get; set; }
    public int GenerateAdvanceDays { get; set; } = 30;
    public bool AutoAssignTeam { get; set; } = false;
    public bool AutoGenerateEnabled { get; set; } = true;
    public DateTime? LastGeneratedDate { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
    public DateTime? NextGenerationDate { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Tạm dừng đến ngày (NULL=không dừng)
    /// </summary>
    public DateTime? PausedUntil { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Template gốc
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ShiftTemplates? ShiftTemplate { get; set; }

    /// <summary>
    /// Team mặc định
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Teams? Team { get; set; }
}