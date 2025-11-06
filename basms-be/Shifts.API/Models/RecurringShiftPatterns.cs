namespace Shifts.API.Models;

/// <summary>
/// RECURRING_SHIFT_PATTERNS - Tự động tạo ca
/// Chức năng: Tự động generate shifts theo pattern (daily, weekly, monthly)
/// Use case: "Mỗi T2-T6 tự động tạo ca sáng 8h-17h tại location A"
/// </summary>
[Table("recurring_shift_patterns")]
public class RecurringShiftPatterns
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Template gốc
    /// </summary>
    public Guid ShiftTemplateId { get; set; }

    /// <summary>
    /// Địa điểm áp dụng
    /// </summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// Team mặc định (optional)
    /// </summary>
    public Guid? TeamId { get; set; }

    // ============================================================================
    // PATTERN DETAILS
    // ============================================================================

    /// <summary>
    /// Tên: "Ca sáng T2-T6 Location A"
    /// </summary>
    public string PatternName { get; set; } = string.Empty;

    /// <summary>
    /// DAILY=hàng ngày | WEEKLY=hàng tuần | BI_WEEKLY=2 tuần 1 lần | MONTHLY=hàng tháng
    /// </summary>
    public string RecurrenceType { get; set; } = string.Empty;

    // ============================================================================
    // WEEKLY PATTERN (cho WEEKLY)
    // ============================================================================

    public bool MondayEnabled { get; set; } = false;
    public bool TuesdayEnabled { get; set; } = false;
    public bool WednesdayEnabled { get; set; } = false;
    public bool ThursdayEnabled { get; set; } = false;
    public bool FridayEnabled { get; set; } = false;
    public bool SaturdayEnabled { get; set; } = false;
    public bool SundayEnabled { get; set; } = false;

    // ============================================================================
    // MONTHLY PATTERN (cho MONTHLY)
    // ============================================================================

    /// <summary>
    /// 1-31 hoặc -1=ngày cuối tháng
    /// </summary>
    public int? MonthlyDayOfMonth { get; set; }

    // ============================================================================
    // EFFECTIVE PERIOD
    // ============================================================================

    /// <summary>
    /// Bắt đầu từ ngày
    /// </summary>
    public DateTime EffectiveStartDate { get; set; }

    /// <summary>
    /// Kết thúc ngày (NULL=vô thời hạn)
    /// </summary>
    public DateTime? EffectiveEndDate { get; set; }

    // ============================================================================
    // GENERATION RULES
    // ============================================================================

    /// <summary>
    /// Tạo trước X ngày (VD: tạo ca tháng sau)
    /// </summary>
    public int GenerateAdvanceDays { get; set; } = 30;

    /// <summary>
    /// Tự động assign team vào ca
    /// </summary>
    public bool AutoAssignTeam { get; set; } = false;

    /// <summary>
    /// Bật tự động tạo
    /// </summary>
    public bool AutoGenerateEnabled { get; set; } = true;

    // ============================================================================
    // LAST GENERATION TRACKING
    // ============================================================================

    /// <summary>
    /// Lần tạo cuối
    /// </summary>
    public DateTime? LastGeneratedDate { get; set; }

    public DateTime? LastGeneratedAt { get; set; }

    /// <summary>
    /// Lần tạo tiếp theo
    /// </summary>
    public DateTime? NextGenerationDate { get; set; }

    // ============================================================================
    // STATUS
    // ============================================================================

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