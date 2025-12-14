using Dapper.Contrib.Extensions;

namespace Attendances.API.Models;

/// <summary>
/// ATTENDANCE_RECORDS - Bảng chấm công chính
/// Chức năng: Lưu thông tin check-in/check-out thực tế của guards
/// Use case: "Guard check-in lúc 8:05, check-out 17:10, tổng 8.5 giờ làm"
/// </summary>
[Table("attendance_records")]
public class AttendanceRecords
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // FOREIGN KEYS
    // ============================================================================

    /// <summary>
    /// Shift assignment tương ứng
    /// </summary>
    public Guid ShiftAssignmentId { get; set; }

    /// <summary>
    /// Guard thực hiện (cached for quick query)
    /// </summary>
    public Guid GuardId { get; set; }

    /// <summary>
    /// Shift ID (cached for quick query)
    /// </summary>
    public Guid ShiftId { get; set; }

    // ============================================================================
    // CHECK-IN INFORMATION
    // ============================================================================

    /// <summary>
    /// Thời gian check-in thực tế
    /// </summary>
    public DateTime CheckInTime { get; set; }

    /// <summary>
    /// Vị trí check-in (latitude)
    /// </summary>
    public decimal? CheckInLatitude { get; set; }

    /// <summary>
    /// Vị trí check-in (longitude)
    /// </summary>
    public decimal? CheckInLongitude { get; set; }

    /// <summary>
    /// Độ chính xác GPS (meters)
    /// </summary>
    public decimal? CheckInLocationAccuracy { get; set; }

    /// <summary>
    /// Khoảng cách từ site (meters)
    /// </summary>
    public decimal? CheckInDistanceFromSite { get; set; }

    /// <summary>
    /// Device ID check-in
    /// </summary>
    public string? CheckInDeviceId { get; set; }

    /// <summary>
    /// S3 URL ảnh khuôn mặt check-in
    /// </summary>
    public string? CheckInFaceImageUrl { get; set; }

    /// <summary>
    /// Điểm match khuôn mặt check-in (0-100%)
    /// </summary>
    public decimal? CheckInFaceMatchScore { get; set; }

    // ============================================================================
    // CHECK-OUT INFORMATION
    // ============================================================================

    /// <summary>
    /// Thời gian check-out thực tế
    /// </summary>
    public DateTime? CheckOutTime { get; set; }

    /// <summary>
    /// Vị trí check-out (latitude)
    /// </summary>
    public decimal? CheckOutLatitude { get; set; }

    /// <summary>
    /// Vị trí check-out (longitude)
    /// </summary>
    public decimal? CheckOutLongitude { get; set; }

    /// <summary>
    /// Độ chính xác GPS (meters)
    /// </summary>
    public decimal? CheckOutLocationAccuracy { get; set; }

    /// <summary>
    /// Khoảng cách từ site (meters)
    /// </summary>
    public decimal? CheckOutDistanceFromSite { get; set; }

    /// <summary>
    /// Device ID check-out
    /// </summary>
    public string? CheckOutDeviceId { get; set; }

    /// <summary>
    /// S3 URL ảnh khuôn mặt check-out
    /// </summary>
    public string? CheckOutFaceImageUrl { get; set; }

    /// <summary>
    /// Điểm match khuôn mặt check-out (0-100%)
    /// </summary>
    public decimal? CheckOutFaceMatchScore { get; set; }

    // ============================================================================
    // SCHEDULED TIME (from shift)
    // ============================================================================

    /// <summary>
    /// Giờ vào dự kiến (từ shift)
    /// </summary>
    public DateTime? ScheduledStartTime { get; set; }

    /// <summary>
    /// Giờ ra dự kiến (từ shift)
    /// </summary>
    public DateTime? ScheduledEndTime { get; set; }

    // ============================================================================
    // DURATION CALCULATIONS
    // ============================================================================

    /// <summary>
    /// Tổng thời gian làm việc (phút)
    /// </summary>
    public int? ActualWorkDurationMinutes { get; set; }

    /// <summary>
    /// Thời gian nghỉ giải lao (phút)
    /// </summary>
    public int BreakDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Tổng giờ làm = (checkout - checkin - break) / 60
    /// </summary>
    public decimal? TotalHours { get; set; }

    // ============================================================================
    // ATTENDANCE STATUS FLAGS
    // ============================================================================

    /// <summary>
    /// Trạng thái: CHECKED_IN | CHECKED_OUT | INCOMPLETE | LATE_CHECKIN | EARLY_CHECKOUT
    /// </summary>
    public string Status { get; set; } = "CHECKED_IN";

    /// <summary>
    /// Đi muộn (check-in sau scheduled start)
    /// </summary>
    public bool IsLate { get; set; } = false;

    /// <summary>
    /// Về sớm (check-out trước scheduled end)
    /// </summary>
    public bool IsEarlyLeave { get; set; } = false;

    /// <summary>
    /// Làm ngoài giờ
    /// </summary>
    public bool HasOvertime { get; set; } = false;

    /// <summary>
    /// Thiếu check-in hoặc check-out
    /// </summary>
    public bool IsIncomplete { get; set; } = false;

    /// <summary>
    /// Đã xác nhận bởi manager
    /// </summary>
    public bool IsVerified { get; set; } = false;

    // ============================================================================
    // LATE/EARLY MINUTES
    // ============================================================================

    /// <summary>
    /// Số phút đi muộn
    /// </summary>
    public int? LateMinutes { get; set; }

    /// <summary>
    /// Số phút về sớm
    /// </summary>
    public int? EarlyLeaveMinutes { get; set; }

    /// <summary>
    /// Số phút làm thêm (nếu có)
    /// </summary>
    public int? OvertimeMinutes { get; set; }

    // ============================================================================
    // VERIFICATION & APPROVAL
    // ============================================================================

    /// <summary>
    /// Manager xác nhận
    /// </summary>
    public Guid? VerifiedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    /// Trạng thái verification: PENDING | APPROVED | REJECTED
    /// </summary>
    public string VerificationStatus { get; set; } = "PENDING";

    // ============================================================================
    // NOTES
    // ============================================================================

    /// <summary>
    /// Ghi chú của guard (lý do đi muộn, v.v.)
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Ghi chú của manager
    /// </summary>
    public string? ManagerNotes { get; set; }

    // ============================================================================
    // AUTO-DETECTION FLAGS
    // ============================================================================

    /// <summary>
    /// Tự động phát hiện bất thường
    /// </summary>
    public bool AutoDetected { get; set; } = false;

    /// <summary>
    /// Đánh dấu cần review
    /// </summary>
    public bool FlagsForReview { get; set; } = false;

    /// <summary>
    /// Lý do cần review
    /// </summary>
    public string? FlagReason { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

}
