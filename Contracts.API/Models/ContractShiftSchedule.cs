namespace Contracts.API.Models;

/// <summary>
/// MẪU LỊCH CA TRONG HỢP ĐỒNG
/// Định nghĩa các shift templates để tự động tạo shifts
/// Ví dụ: Ca sáng 8h-17h T2-T6, Ca đêm 22h-6h 24/7...
/// </summary>
[Table("contract_shift_schedules")]
public class ContractShiftSchedule
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Thuộc hợp đồng nào
    /// </summary>
    public Guid ContractId { get; set; }

    /// <summary>
    /// Áp dụng cho địa điểm cụ thể nào? (NULL = all locations in contract)
    /// Link trực tiếp đến CustomerLocation.Id
    /// </summary>
    public Guid? LocationId { get; set; }

    /// <summary>
    /// Tên mẫu ca: "Morning Shift", "Night Patrol", "Weekend Coverage"
    /// </summary>
    public string ScheduleName { get; set; } = string.Empty;

    /// <summary>
    /// Loại ca: regular, overtime, standby, emergency, event
    /// </summary>
    public string ScheduleType { get; set; } = "regular";

    // ============================================================================
    // THỜI GIAN CA
    // ============================================================================

    /// <summary>
    /// Giờ bắt đầu ca: 08:00:00
    /// </summary>
    public TimeSpan ShiftStartTime { get; set; }

    /// <summary>
    /// Giờ kết thúc ca: 17:00:00
    /// </summary>
    public TimeSpan ShiftEndTime { get; set; }

    /// <summary>
    /// Ca có qua đêm không? (22:00-06:00)
    /// </summary>
    public bool CrossesMidnight { get; set; } = false;

    /// <summary>
    /// Thời lượng ca (giờ): 8.0, 12.0...
    /// </summary>
    public decimal DurationHours { get; set; }

    /// <summary>
    /// Thời gian nghỉ giải lao (phút): 60
    /// </summary>
    public int BreakMinutes { get; set; } = 0;

    // ============================================================================
    // NHÂN SỰ
    // ============================================================================

    /// <summary>
    /// Số lượng bảo vệ cần cho ca này: 2, 3, 5...
    /// </summary>
    public int GuardsPerShift { get; set; }

    // ============================================================================
    // MẪU LẶP LẠI
    // ============================================================================

    /// <summary>
    /// Loại lặp lại: daily, weekly, bi_weekly, monthly, specific_dates
    /// </summary>
    public string RecurrenceType { get; set; } = "weekly";

    /// <summary>
    /// Áp dụng cho Thứ 2?
    /// </summary>
    public bool AppliesMonday { get; set; } = false;

    /// <summary>
    /// Áp dụng cho Thứ 3?
    /// </summary>
    public bool AppliesTuesday { get; set; } = false;

    /// <summary>
    /// Áp dụng cho Thứ 4?
    /// </summary>
    public bool AppliesWednesday { get; set; } = false;

    /// <summary>
    /// Áp dụng cho Thứ 5?
    /// </summary>
    public bool AppliesThursday { get; set; } = false;

    /// <summary>
    /// Áp dụng cho Thứ 6?
    /// </summary>
    public bool AppliesFriday { get; set; } = false;

    /// <summary>
    /// Áp dụng cho Thứ 7?
    /// </summary>
    public bool AppliesSaturday { get; set; } = false;

    /// <summary>
    /// Áp dụng cho Chủ nhật?
    /// </summary>
    public bool AppliesSunday { get; set; } = false;

    /// <summary>
    /// Các ngày trong tháng (nếu recurrence = monthly)
    /// Ví dụ: "1,15" = ngày 1 và 15 hàng tháng
    /// </summary>
    public string? MonthlyDates { get; set; }

    // ============================================================================
    // XỬ LÝ NGÀY ĐẶC BIỆT
    // ============================================================================

    /// <summary>
    /// Có áp dụng vào ngày lễ quốc gia không?
    /// </summary>
    public bool AppliesOnPublicHolidays { get; set; } = true;

    /// <summary>
    /// Có áp dụng vào ngày nghỉ của khách hàng không?
    /// </summary>
    public bool AppliesOnCustomerHolidays { get; set; } = true;

    /// <summary>
    /// Có áp dụng vào cuối tuần không?
    /// </summary>
    public bool AppliesOnWeekends { get; set; } = true;

    /// <summary>
    /// Bỏ qua khi location đóng cửa?
    /// false = vẫn tạo ca (bảo vệ canh khi đóng cửa)
    /// </summary>
    public bool SkipWhenLocationClosed { get; set; } = false;

    // ============================================================================
    // YÊU CẦU BẢO VỆ
    // ============================================================================

    /// <summary>
    /// Yêu cầu bảo vệ có vũ trang?
    /// </summary>
    public bool RequiresArmedGuard { get; set; } = false;

    /// <summary>
    /// Yêu cầu supervisor?
    /// </summary>
    public bool RequiresSupervisor { get; set; } = false;

    /// <summary>
    /// Kinh nghiệm tối thiểu (tháng): 0, 6, 12...
    /// </summary>
    public int MinimumExperienceMonths { get; set; } = 0;

    /// <summary>
    /// Chứng chỉ yêu cầu (JSON array): ["First Aid", "Fire Safety"]
    /// </summary>
    public string? RequiredCertifications { get; set; }

    // ============================================================================
    // CÀI ĐẶT TỰ ĐỘNG TẠO CA
    // ============================================================================

    /// <summary>
    /// Bật tự động tạo ca?
    /// </summary>
    public bool AutoGenerateEnabled { get; set; } = true;

    /// <summary>
    /// Tạo ca trước bao nhiêu ngày: 30 ngày
    /// </summary>
    public int GenerateAdvanceDays { get; set; } = 30;

    // ============================================================================
    // THỜI GIAN HIỆU LỰC
    // ============================================================================

    /// <summary>
    /// Có hiệu lực từ ngày
    /// </summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// Có hiệu lực đến ngày (NULL = vô thời hạn)
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    // ============================================================================
    // METADATA
    // ============================================================================

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
