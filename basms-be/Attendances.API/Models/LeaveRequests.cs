using Dapper.Contrib.Extensions;

namespace Attendances.API.Models;

/// <summary>
/// LEAVE_REQUESTS - Bảng quản lý đơn xin nghỉ
/// Chức năng: Guards đăng ký nghỉ phép, nghỉ ốm, nghỉ không lương
/// Use case: "Guard xin nghỉ phép 3 ngày từ 15-17/01, manager approve"
/// </summary>
[Table("leave_requests")]
public class LeaveRequests
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // FOREIGN KEYS
    // ============================================================================

    /// <summary>
    /// Guard xin nghỉ
    /// </summary>
    public Guid GuardId { get; set; }

    /// <summary>
    /// Team ID (cached for reporting)
    /// </summary>
    public Guid? TeamId { get; set; }

    /// <summary>
    /// Guard nhận bàn giao công việc
    /// </summary>
    public Guid? HandoverToGuardId { get; set; }

    // ============================================================================
    // LEAVE TYPE & CLASSIFICATION
    // ============================================================================

    /// <summary>
    /// Loại nghỉ:
    /// ANNUAL_LEAVE = Nghỉ phép năm (có lương)
    /// SICK_LEAVE = Nghỉ ốm (có lương theo quy định)
    /// UNPAID_LEAVE = Nghỉ không lương
    /// MATERNITY_LEAVE = Nghỉ thai sản
    /// PATERNITY_LEAVE = Nghỉ chế độ (cha)
    /// BEREAVEMENT_LEAVE = Nghỉ tang
    /// MARRIAGE_LEAVE = Nghỉ cưới
    /// EMERGENCY_LEAVE = Nghỉ khẩn cấp
    /// OTHER = Khác
    /// </summary>
    public string LeaveType { get; set; } = string.Empty;

    /// <summary>
    /// Phạm vi: FULL_DAY | HALF_DAY_MORNING | HALF_DAY_AFTERNOON
    /// </summary>
    public string LeaveScale { get; set; } = "FULL_DAY";

    // ============================================================================
    // TIME PERIOD
    // ============================================================================

    /// <summary>
    /// Ngày bắt đầu nghỉ
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Ngày kết thúc nghỉ
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Thời gian bắt đầu cụ thể (cho half-day)
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Thời gian kết thúc cụ thể (cho half-day)
    /// </summary>
    public DateTime? EndTime { get; set; }

    // ============================================================================
    // DURATION CALCULATIONS
    // ============================================================================

    /// <summary>
    /// Tổng số ngày nghỉ (calendar days)
    /// </summary>
    public int TotalDays { get; set; }

    /// <summary>
    /// Số ngày làm việc nghỉ (exclude weekends/holidays)
    /// </summary>
    public decimal TotalWorkDays { get; set; }

    /// <summary>
    /// Tổng giờ nghỉ
    /// </summary>
    public decimal? TotalHours { get; set; }

    // ============================================================================
    // REASON & DOCUMENTATION
    // ============================================================================

    /// <summary>
    /// Lý do nghỉ (bắt buộc)
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// URL tài liệu đính kèm (S3): giấy khám bệnh, đơn xin nghỉ scan
    /// </summary>
    public string? SupportingDocumentUrl { get; set; }

    /// <summary>
    /// Ghi chú thêm
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Ghi chú từ manager
    /// </summary>
    public string? ManagerNotes { get; set; }

    // ============================================================================
    // HANDOVER (for important roles)
    // ============================================================================

    /// <summary>
    /// Có bàn giao công việc không
    /// </summary>
    public bool HasHandover { get; set; } = false;

    /// <summary>
    /// Guard thay thế (nếu có)
    /// </summary>
    public Guid? ReplacementGuardId { get; set; }

    /// <summary>
    /// Ghi chú bàn giao
    /// </summary>
    public string? HandoverNotes { get; set; }

    // ============================================================================
    // CONTACT DURING LEAVE
    // ============================================================================

    /// <summary>
    /// SĐT liên hệ trong thời gian nghỉ
    /// </summary>
    public string? ContactDuringLeave { get; set; }

    /// <summary>
    /// Email liên hệ khẩn cấp
    /// </summary>
    public string? EmergencyContact { get; set; }

    // ============================================================================
    // APPROVAL WORKFLOW
    // ============================================================================

    /// <summary>
    /// Trạng thái: PENDING | APPROVED | REJECTED | CANCELLED | WITHDRAWN
    /// </summary>
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// Thời gian submit đơn
    /// </summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>
    /// Manager phê duyệt
    /// </summary>
    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Lý do từ chối
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Thời gian hủy/rút đơn
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Lý do hủy
    /// </summary>
    public string? CancellationReason { get; set; }

    // ============================================================================
    // PAYROLL IMPACT (Reference - actual calculation in Payroll Service)
    // ============================================================================

    /// <summary>
    /// Nghỉ có lương
    /// </summary>
    public bool IsPaid { get; set; } = true;

    /// <summary>
    /// Tỷ lệ trả lương: 100%, 75%, 0%
    /// </summary>
    public decimal PaymentPercentage { get; set; } = 100m;

    /// <summary>
    /// Ảnh hưởng đến phép năm còn lại
    /// </summary>
    public bool DeductsFromAnnualLeave { get; set; } = false;

    /// <summary>
    /// Số ngày phép bị trừ
    /// </summary>
    public decimal? AnnualLeaveDaysDeducted { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Optimistic locking
    /// </summary>
    public int Version { get; set; } = 1;

}
