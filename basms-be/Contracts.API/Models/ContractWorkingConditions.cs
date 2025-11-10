namespace Contracts.API.Models;

/// <summary>
/// ĐIỀU KIỆN LÀM VIỆC TRONG HỢP ĐỒNG
/// Định nghĩa các quy định về giờ làm, ca trực, nghỉ ngơi, và các điều kiện làm việc khác
/// KHÔNG BAO GỒM: Lương, hệ số lương, phụ cấp (thuộc về Payroll Service)
/// </summary>
[Table("contract_working_conditions")]
public class ContractWorkingConditions
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Thuộc hợp đồng nào
    /// </summary>
    public Guid ContractId { get; set; }

    // ============================================================================
    // GIỜ LÀM VIỆC CHUẨN
    // ============================================================================

    /// <summary>
    /// Số giờ làm việc tiêu chuẩn/ngày: 8, 9, 12
    /// </summary>
    public decimal? StandardHoursPerDay { get; set; }

    /// <summary>
    /// Số giờ làm việc tiêu chuẩn/tuần: 40, 44, 48
    /// </summary>
    public decimal? StandardHoursPerWeek { get; set; }

    /// <summary>
    /// Số giờ làm việc tiêu chuẩn/tháng: 160, 176, 192
    /// </summary>
    public decimal? StandardHoursPerMonth { get; set; }

    // ============================================================================
    // GIỚI HẠN TĂNG CA
    // ============================================================================

    /// <summary>
    /// Số giờ tăng ca tối đa/ngày: 4, 6, 8
    /// Theo luật lao động VN: tối đa 4h/ngày
    /// </summary>
    public decimal? MaxOvertimeHoursPerDay { get; set; }

    /// <summary>
    /// Số giờ tăng ca tối đa/tháng: 30, 40, 60
    /// Theo luật lao động VN: tối đa 30h/tháng (hoặc 40h với một số ngành)
    /// </summary>
    public decimal? MaxOvertimeHoursPerMonth { get; set; }

    /// <summary>
    /// Số giờ tăng ca tối đa/năm: 200, 300
    /// Theo luật lao động VN: tối đa 200h/năm (hoặc 300h với một số ngành)
    /// </summary>
    public decimal? MaxOvertimeHoursPerYear { get; set; }

    /// <summary>
    /// Cho phép tăng ca cuối tuần? true/false
    /// </summary>
    public bool AllowOvertimeOnWeekends { get; set; }

    /// <summary>
    /// Cho phép tăng ca ngày lễ? true/false
    /// </summary>
    public bool AllowOvertimeOnHolidays { get; set; }

    /// <summary>
    /// Yêu cầu phê duyệt tăng ca trước? true/false
    /// </summary>
    public bool RequireOvertimeApproval { get; set; }

    // ============================================================================
    // CA ĐÊM
    // ============================================================================

    /// <summary>
    /// Giờ bắt đầu ca đêm: 22:00, 23:00
    /// Theo luật lao động VN: 22:00 - 06:00
    /// </summary>
    public TimeSpan? NightShiftStartTime { get; set; }

    /// <summary>
    /// Giờ kết thúc ca đêm: 06:00, 07:00
    /// </summary>
    public TimeSpan? NightShiftEndTime { get; set; }

    /// <summary>
    /// Số giờ tối thiểu để tính là ca đêm: 2, 4
    /// Ví dụ: nếu chỉ làm 1h trong khung 22h-6h thì không tính ca đêm
    /// </summary>
    public decimal? MinimumNightShiftHours { get; set; }

    // ============================================================================
    // CA TRỰC LIÊN TỤC
    // ============================================================================

    /// <summary>
    /// Cho phép ca trực 24h? true/false
    /// </summary>
    public bool AllowContinuous24hShift { get; set; }

    /// <summary>
    /// Cho phép ca trực 48h? true/false
    /// </summary>
    public bool AllowContinuous48hShift { get; set; }

    /// <summary>
    /// Có tính giờ ngủ trong ca trực liên tục? true/false
    /// Ví dụ: Ca trực 24h, có 8h ngủ nghỉ
    /// </summary>
    public bool CountSleepTimeInContinuousShift { get; set; }

    /// <summary>
    /// Tỷ lệ tính giờ ngủ: 0.5 (50%), 0.7 (70%)
    /// Ví dụ: 8h ngủ x 70% = 5.6h được tính công
    /// </summary>
    public decimal? SleepTimeCalculationRatio { get; set; }

    /// <summary>
    /// Số giờ nghỉ tối thiểu giữa 2 ca: 8, 11, 12
    /// Theo luật lao động VN: tối thiểu 12h nghỉ giữa 2 ca
    /// </summary>
    public decimal? MinimumRestHoursBetweenShifts { get; set; }

    // ============================================================================
    // NGÀY NGHỈ & NGÀY LỄ
    // ============================================================================

    /// <summary>
    /// Số ngày nghỉ phép/năm: 12, 14, 15
    /// Theo luật lao động VN: 12 ngày (tăng theo thâm niên)
    /// </summary>
    public int? AnnualLeaveDays { get; set; }

    /// <summary>
    /// Danh sách ngày Tết (JSON array): ["2025-01-29", "2025-01-30", ...]
    /// Thông thường 5-7 ngày Tết Nguyên Đán
    /// </summary>
    public string? TetHolidayDates { get; set; }

    /// <summary>
    /// Danh sách ngày lễ địa phương (JSON array)
    /// Ví dụ: Giỗ tổ Hùng Vương, Giải phóng miền Nam...
    /// </summary>
    public string? LocalHolidaysList { get; set; }

    /// <summary>
    /// Cách tính khi lễ trùng cuối tuần: "max", "cumulative", "replace"
    /// - max: Lấy hệ số cao nhất (lễ HOẶC cuối tuần)
    /// - cumulative: Cộng dồn (lễ VÀ cuối tuần)
    /// - replace: Bù ngày nghỉ khác
    /// </summary>
    public string? HolidayWeekendCalculationMethod { get; set; }

    /// <summary>
    /// Thứ 7 là ngày làm việc thường? true/false
    /// Nếu false: Thứ 7 là ngày cuối tuần
    /// </summary>
    public bool SaturdayAsRegularWorkday { get; set; }

    // ============================================================================
    // CHÍNH SÁCH VI PHẠM
    // ============================================================================

    /// <summary>
    /// Chính sách khi vượt giới hạn tăng ca: "block", "warning", "compensate"
    /// - block: Không cho phép
    /// - warning: Cảnh báo nhưng vẫn cho phép
    /// - compensate: Cho phép nhưng phải bồi thường
    /// </summary>
    public string? OvertimeLimitViolationPolicy { get; set; }

    /// <summary>
    /// Chính sách tăng ca không phê duyệt: "block", "penalty", "allow"
    /// - block: Không tính công
    /// - penalty: Tính công nhưng bị phạt
    /// - allow: Chấp nhận
    /// </summary>
    public string? UnapprovedOvertimePolicy { get; set; }

    /// <summary>
    /// Chính sách khi nghỉ giữa ca < quy định: "block", "compensate", "allow"
    /// Ví dụ: Nghỉ 8h thay vì 12h
    /// </summary>
    public string? InsufficientRestPolicy { get; set; }

    // ============================================================================
    // CA ĐẶC BIỆT
    // ============================================================================

    /// <summary>
    /// Cho phép ca sự kiện (event shift)? true/false
    /// Ví dụ: Lễ hội, sự kiện đặc biệt, ca bảo vệ concert...
    /// </summary>
    public bool AllowEventShift { get; set; }

    /// <summary>
    /// Cho phép ca khẩn cấp (emergency call)? true/false
    /// Ví dụ: Gọi bảo vệ đến ngay lập tức khi có sự cố
    /// </summary>
    public bool AllowEmergencyCall { get; set; }

    /// <summary>
    /// Cho phép ca thay thế (replacement shift)? true/false
    /// Ví dụ: Thay người bị ốm, đột xuất
    /// </summary>
    public bool AllowReplacementShift { get; set; }

    /// <summary>
    /// Thời gian thông báo tối thiểu cho ca khẩn cấp (phút): 30, 60, 120
    /// </summary>
    public int? MinimumEmergencyNoticeMinutes { get; set; }

    // ============================================================================
    // GHI CHÚ
    // ============================================================================

    /// <summary>
    /// Ghi chú điều kiện làm việc chung
    /// </summary>
    public string? GeneralNotes { get; set; }

    /// <summary>
    /// Ghi chú về các điều khoản đặc biệt
    /// </summary>
    public string? SpecialTerms { get; set; }

    // ============================================================================
    // TRẠNG THÁI & THỜI GIAN
    // ============================================================================

    /// <summary>
    /// Điều kiện này có đang active? (dùng cho amendments)
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Ngày có hiệu lực
    /// </summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// Ngày hết hiệu lực (NULL = vô thời hạn)
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid UpdatedBy { get; set; }
}
