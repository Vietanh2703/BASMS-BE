using Dapper.Contrib.Extensions;

namespace Attendances.API.Models;

/// <summary>
/// ATTENDANCE_EXCEPTIONS - Bảng xử lý ngoại lệ chấm công
/// Chức năng: Lưu các trường hợp bất thường: quên check-in/out, lỗi thiết bị, v.v.
/// Use case: "Guard quên check-out, manager approve manual checkout"
/// </summary>
[Table("attendance_exceptions")]
public class AttendanceExceptions
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // FOREIGN KEYS
    // ============================================================================

    /// <summary>
    /// Attendance record liên quan (nullable cho trường hợp chưa có record)
    /// </summary>
    public Guid? AttendanceRecordId { get; set; }

    /// <summary>
    /// Shift assignment
    /// </summary>
    public Guid ShiftAssignmentId { get; set; }

    /// <summary>
    /// Guard có ngoại lệ
    /// </summary>
    public Guid GuardId { get; set; }

    /// <summary>
    /// Shift ID
    /// </summary>
    public Guid ShiftId { get; set; }

    // ============================================================================
    // EXCEPTION INFORMATION
    // ============================================================================

    /// <summary>
    /// Loại ngoại lệ:
    /// MISSING_CHECKIN | MISSING_CHECKOUT | DEVICE_ERROR | LOCATION_ERROR |
    /// MANUAL_OVERRIDE | SYSTEM_ERROR | OTHER
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>
    /// Mức độ nghiêm trọng: LOW | MEDIUM | HIGH | CRITICAL
    /// </summary>
    public string Severity { get; set; } = "MEDIUM";

    /// <summary>
    /// Mô tả chi tiết ngoại lệ
    /// </summary>
    public string Description { get; set; } = string.Empty;

    // ============================================================================
    // DETECTION
    // ============================================================================

    /// <summary>
    /// Tự động phát hiện hay manual report
    /// </summary>
    public bool AutoDetected { get; set; } = true;

    /// <summary>
    /// Thời điểm phát hiện
    /// </summary>
    public DateTime DetectedAt { get; set; }

    // ============================================================================
    // RESOLUTION
    // ============================================================================

    /// <summary>
    /// Trạng thái: OPEN | IN_PROGRESS | RESOLVED | REJECTED | CANCELLED
    /// </summary>
    public string Status { get; set; } = "OPEN";

    /// <summary>
    /// Thời điểm giải quyết
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Manager giải quyết
    /// </summary>
    public Guid? ResolvedBy { get; set; }

    /// <summary>
    /// Ghi chú giải quyết
    /// </summary>
    public string? ResolutionNotes { get; set; }

    /// <summary>
    /// Hành động đã thực hiện: MANUAL_CHECKIN | MANUAL_CHECKOUT | APPROVE_OVERRIDE | REJECT | NONE
    /// </summary>
    public string? ResolutionAction { get; set; }

    // ============================================================================
    // SUGGESTED CORRECTION (for auto-resolution)
    // ============================================================================

    /// <summary>
    /// Có thể tự động giải quyết
    /// </summary>
    public bool AutoResolvable { get; set; } = false;

    /// <summary>
    /// Đề xuất hành động
    /// </summary>
    public string? SuggestedAction { get; set; }

    /// <summary>
    /// Template message cho guard/manager
    /// </summary>
    public string? NotificationTemplate { get; set; }

    // ============================================================================
    // PENALTY/IMPACT
    // ============================================================================

    /// <summary>
    /// Số tiền phạt (nếu có) - VNĐ
    /// </summary>
    public decimal? PenaltyAmount { get; set; }

    /// <summary>
    /// Giảm điểm đánh giá
    /// </summary>
    public decimal? PerformanceImpact { get; set; }

    /// <summary>
    /// Ghi chú về ảnh hưởng
    /// </summary>
    public string? ImpactNotes { get; set; }

    // ============================================================================
    // APPROVAL WORKFLOW
    // ============================================================================

    /// <summary>
    /// Cần phê duyệt từ cấp cao hơn
    /// </summary>
    public bool RequiresApproval { get; set; } = true;

    /// <summary>
    /// Người phê duyệt
    /// </summary>
    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// PENDING | APPROVED | REJECTED
    /// </summary>
    public string ApprovalStatus { get; set; } = "PENDING";

    public string? RejectionReason { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

}
