using Dapper.Contrib.Extensions;

namespace Shifts.API.Models;

/// <summary>
/// SHIFT_TEMPLATES - Mẫu ca trực (Reusable)
/// Chức năng: Định nghĩa mẫu ca tái sử dụng, auto-generate shifts
/// Use case: "Ca sáng 8h-17h, áp dụng T2-T6, cần 2 guards"
/// </summary>
[Table("shift_templates")]
public class ShiftTemplates
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // FOREIGN KEYS
    // ============================================================================

    /// <summary>
    /// Manager ID - Quản lý/tạo template này
    /// Link với bảng managers để tracking ownership
    /// </summary>
    public Guid? ManagerId { get; set; }

    /// <summary>
    /// Contract ID từ Contracts.API
    /// Link template với contract để tracking và auto-generation
    /// </summary>
    public Guid? ContractId { get; set; }

    /// <summary>
    /// Team ID - Team được auto-assign khi generate shifts từ template này
    /// NULL = không auto-assign, manager phải assign thủ công
    /// </summary>
    public Guid? TeamId { get; set; }

    // ============================================================================
    // IDENTITY
    // ============================================================================

    /// <summary>
    /// Mã: MORNING-8H, NIGHT-12H
    /// </summary>
    public string TemplateCode { get; set; } = string.Empty;

    /// <summary>
    /// Tên: "Ca Sáng Hành Chính"
    /// </summary>
    public string TemplateName { get; set; } = string.Empty;

    public string? Description { get; set; }

    // ============================================================================
    // THỜI GIAN CA
    // ============================================================================

    /// <summary>
    /// Giờ bắt đầu: 08:00
    /// </summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>
    /// Giờ kết thúc: 17:00
    /// </summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// Thời lượng: 8.00h = end - start
    /// </summary>
    public decimal DurationHours { get; set; }

    // ============================================================================
    // NGHỈ GIẢI LAO
    // ============================================================================

    /// <summary>
    /// Tổng nghỉ: 60 phút
    /// </summary>
    public int BreakDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Nghỉ có lương: 0
    /// </summary>
    public int PaidBreakMinutes { get; set; } = 0;

    /// <summary>
    /// Nghỉ trừ vào công: 60 phút
    /// </summary>
    public int UnpaidBreakMinutes { get; set; } = 60;

    // ============================================================================
    // PHÂN LOẠI CA
    // ============================================================================

    /// <summary>
    /// Ca đêm (22h-6h) → +30% lương
    /// </summary>
    public bool IsNightShift { get; set; } = false;

    /// <summary>
    /// Ca qua đêm: 22h hôm trước → 6h sáng
    /// </summary>
    public bool IsOvernight { get; set; } = false;

    /// <summary>
    /// Cờ kỹ thuật: ca cross midnight
    /// </summary>
    public bool CrossesMidnight { get; set; } = false;

    // ============================================================================
    // ÁP DỤNG CHO NGÀY NÀO TRONG TUẦN
    // ============================================================================

    /// <summary>
    /// Áp dụng Thứ 2
    /// </summary>
    public bool AppliesMonday { get; set; } = false;

    /// <summary>
    /// Áp dụng Thứ 3
    /// </summary>
    public bool AppliesTuesday { get; set; } = false;

    /// <summary>
    /// Áp dụng Thứ 4
    /// </summary>
    public bool AppliesWednesday { get; set; } = false;

    /// <summary>
    /// Áp dụng Thứ 5
    /// </summary>
    public bool AppliesThursday { get; set; } = false;

    /// <summary>
    /// Áp dụng Thứ 6
    /// </summary>
    public bool AppliesFriday { get; set; } = false;

    /// <summary>
    /// Áp dụng Thứ 7
    /// </summary>
    public bool AppliesSaturday { get; set; } = false;

    /// <summary>
    /// Áp dụng CN
    /// </summary>
    public bool AppliesSunday { get; set; } = false;

    // ============================================================================
    // YÊU CẦU NHÂN SỰ
    // ============================================================================

    /// <summary>
    /// Số guards tối thiểu
    /// </summary>
    public int MinGuardsRequired { get; set; } = 1;

    /// <summary>
    /// Số guards tối đa
    /// </summary>
    public int? MaxGuardsAllowed { get; set; }

    /// <summary>
    /// Số guards tối ưu (gợi ý)
    /// </summary>
    public int? OptimalGuards { get; set; }

    // ============================================================================
    // THÔNG TIN LOCATION (Từ Contract)
    // ============================================================================

    /// <summary>
    /// LocationId từ customer_locations (để generate shifts cho location cụ thể)
    /// </summary>
    public Guid? LocationId { get; set; }

    /// <summary>
    /// Tên location (cached từ customer_locations để dễ query)
    /// </summary>
    public string? LocationName { get; set; }

    /// <summary>
    /// Địa chỉ location
    /// </summary>
    public string? LocationAddress { get; set; }

    /// <summary>
    /// Vĩ độ GPS (để check-in/check-out)
    /// </summary>
    public decimal? LocationLatitude { get; set; }

    /// <summary>
    /// Kinh độ GPS (để check-in/check-out)
    /// </summary>
    public decimal? LocationLongitude { get; set; }

    // ============================================================================
    // TRẠNG THÁI
    // ============================================================================

    /// <summary>
    /// Trạng thái template: await_create_shift, active, inactive, archived
    /// await_create_shift = đã import từ contract, chờ tạo shifts
    /// </summary>
    public string Status { get; set; } = "await_create_shift";

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Hiệu lực từ ngày
    /// </summary>
    public DateTime? EffectiveFrom { get; set; }

    /// <summary>
    /// Hết hiệu lực (NULL=vô thời hạn)
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Shifts created from this template
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<Shifts> Shifts { get; set; } = new List<Shifts>();

    /// <summary>
    /// Recurring patterns using this template
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<RecurringShiftPatterns> RecurringPatterns { get; set; } = new List<RecurringShiftPatterns>();
}