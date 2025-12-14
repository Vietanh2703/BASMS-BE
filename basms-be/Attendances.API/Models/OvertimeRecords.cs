using Dapper.Contrib.Extensions;

namespace Attendances.API.Models;

/// <summary>
/// OVERTIME_RECORDS - Bảng quản lý tăng ca
/// Chức năng: Lưu thông tin làm thêm giờ, tính công OT theo quy định
/// Use case: "Guard làm thêm 2 giờ sau ca, cần approve và tính lương 150%"
/// </summary>
[Table("overtime_records")]
public class OvertimeRecords
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // FOREIGN KEYS
    // ============================================================================

    /// <summary>
    /// Attendance record gốc
    /// </summary>
    public Guid AttendanceRecordId { get; set; }

    /// <summary>
    /// Guard làm tăng ca
    /// </summary>
    public Guid GuardId { get; set; }

    /// <summary>
    /// Shift ID
    /// </summary>
    public Guid ShiftId { get; set; }

    // ============================================================================
    // OVERTIME TYPE & CLASSIFICATION
    // ============================================================================

    /// <summary>
    /// Loại tăng ca:
    /// REGULAR_OT = Ngày thường sau giờ
    /// WEEKEND_OT = Thứ 7, Chủ nhật
    /// HOLIDAY_OT = Ngày lễ
    /// NIGHT_OT = Ca đêm (22h-6h)
    /// </summary>
    public string OvertimeType { get; set; } = "REGULAR_OT";

    // ============================================================================
    // TIME INFORMATION
    // ============================================================================

    /// <summary>
    /// Giờ bắt đầu OT dự kiến
    /// </summary>
    public DateTime PlannedOvertimeStart { get; set; }

    /// <summary>
    /// Giờ kết thúc OT dự kiến
    /// </summary>
    public DateTime PlannedOvertimeEnd { get; set; }

    /// <summary>
    /// Giờ bắt đầu OT thực tế
    /// </summary>
    public DateTime? ActualOvertimeStart { get; set; }

    /// <summary>
    /// Giờ kết thúc OT thực tế
    /// </summary>
    public DateTime? ActualOvertimeEnd { get; set; }

    // ============================================================================
    // DURATION CALCULATIONS
    // ============================================================================

    /// <summary>
    /// Số phút OT dự kiến
    /// </summary>
    public int PlannedOvertimeMinutes { get; set; }

    /// <summary>
    /// Số phút OT thực tế
    /// </summary>
    public int? ActualOvertimeMinutes { get; set; }

    /// <summary>
    /// Số giờ OT thực tế (decimal)
    /// </summary>
    public decimal? ActualOvertimeHours { get; set; }

    // ============================================================================
    // PAY RATE & CALCULATION (Reference only - actual payroll in Payroll Service)
    // ============================================================================

    /// <summary>
    /// Hệ số lương OT: 150%, 200%, 300%
    /// Reference từ Labor Code:
    /// - Ngày thường: 150%
    /// - Thứ 7, CN: 200%
    /// - Ngày lễ: 300%
    /// </summary>
    public decimal OvertimeRate { get; set; } = 1.5m;

    /// <summary>
    /// Tham chiếu để tính lương (VNĐ/giờ)
    /// NOTE: Chỉ để tham khảo, payroll thực tế do Payroll Service xử lý
    /// </summary>
    public decimal? BaseHourlyRate { get; set; }

    // ============================================================================
    // APPROVAL WORKFLOW
    // ============================================================================

    /// <summary>
    /// Trạng thái: PENDING | APPROVED | REJECTED | CANCELLED
    /// </summary>
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// Manager yêu cầu OT
    /// </summary>
    public Guid RequestedBy { get; set; }

    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// Manager phê duyệt
    /// </summary>
    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Lý do từ chối
    /// </summary>
    public string? RejectionReason { get; set; }

    // ============================================================================
    // REASON & JUSTIFICATION
    // ============================================================================

    /// <summary>
    /// Lý do cần OT: Thiếu người, khẩn cấp, dự án đặc biệt, v.v.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Ghi chú thêm
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Ghi chú từ manager
    /// </summary>
    public string? ManagerNotes { get; set; }

    // ============================================================================
    // FLAGS
    // ============================================================================

    /// <summary>
    /// OT bắt buộc (không thể từ chối)
    /// </summary>
    public bool IsMandatory { get; set; } = false;

    /// <summary>
    /// OT khẩn cấp
    /// </summary>
    public bool IsEmergency { get; set; } = false;

    /// <summary>
    /// Đã trả lương OT
    /// </summary>
    public bool IsPaid { get; set; } = false;

    public DateTime? PaidAt { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

}
