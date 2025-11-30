namespace Shifts.API.Models;

/// <summary>
/// SHIFT_ASSIGNMENTS - Phân công guards vào ca (Many-to-Many: Shifts ↔ Guards)
/// Chức năng: Gán guards vào shifts, track status lifecycle, link với attendance
/// Use case: "Gán Guard A vào ca ngày 15/01, guard xác nhận, đã check-in"
/// </summary>
[Table("shift_assignments")]
public class ShiftAssignments
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // FOREIGN KEYS
    // ============================================================================

    /// <summary>
    /// Ca trực nào
    /// </summary>
    public Guid ShiftId { get; set; }

    /// <summary>
    /// Team nào (NULL=cá nhân)
    /// </summary>
    public Guid? TeamId { get; set; }

    /// <summary>
    /// Guard nào
    /// </summary>
    public Guid GuardId { get; set; }

    // ============================================================================
    // ASSIGNMENT CONTEXT
    // ============================================================================

    /// <summary>
    /// REGULAR=bình thường | OVERTIME=tăng ca | REPLACEMENT=thay thế | EMERGENCY=khẩn cấp | VOLUNTARY=tự nguyện | MANDATORY=bắt buộc
    /// </summary>
    public string AssignmentType { get; set; } = "REGULAR";

    // ============================================================================
    // REPLACEMENT INFO (nếu thay guard khác)
    // ============================================================================

    /// <summary>
    /// Thay thế guard nào
    /// </summary>
    public Guid? ReplacedGuardId { get; set; }

    /// <summary>
    /// Lý do thay: "Guard A ốm đột xuất"
    /// </summary>
    public string? ReplacementReason { get; set; }

    public bool IsReplacement { get; set; } = false;

    // ============================================================================
    // STATUS LIFECYCLE - THEO DÕI TRẠNG THÁI
    // ============================================================================

    /// <summary>
    /// ASSIGNED=đã giao | CONFIRMED=guard xác nhận | DECLINED=từ chối | CHECKED_IN=đã vào ca | CHECKED_OUT=đã ra ca | COMPLETED=hoàn thành | NO_SHOW=không đến | CANCELLED=hủy
    /// </summary>
    public string Status { get; set; } = "ASSIGNED";

    // ============================================================================
    // STATUS TIMESTAMPS (track từng bước)
    // ============================================================================

    /// <summary>
    /// Thời điểm giao ca
    /// </summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// Guard xác nhận sẵn sàng
    /// </summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>
    /// Guard từ chối
    /// </summary>
    public DateTime? DeclinedAt { get; set; }

    /// <summary>
    /// Guard check-in
    /// </summary>
    public DateTime? CheckedInAt { get; set; }

    /// <summary>
    /// Guard check-out
    /// </summary>
    public DateTime? CheckedOutAt { get; set; }

    /// <summary>
    /// Ca hoàn thành
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Hủy assignment
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    // ============================================================================
    // REASONS
    // ============================================================================

    /// <summary>
    /// Lý do từ chối: "Bận việc gia đình"
    /// </summary>
    public string? DeclineReason { get; set; }

    /// <summary>
    /// Lý do hủy
    /// </summary>
    public string? CancellationReason { get; set; }

    // ============================================================================
    // LINK VỚI ATTENDANCE SERVICE
    // ============================================================================

    /// <summary>
    /// Link sang attendance_records sau khi check-in
    /// </summary>
    public Guid? AttendanceRecordId { get; set; }

    /// <summary>
    /// Đã sync sang Attendance Service
    /// </summary>
    public bool AttendanceSynced { get; set; } = false;

    // ============================================================================
    // NOTIFICATIONS - THÔNG BÁO CHO GUARDS
    // ============================================================================

    /// <summary>
    /// Đã gửi thông báo
    /// </summary>
    public bool NotificationSent { get; set; } = false;

    public DateTime? NotificationSentAt { get; set; }

    /// <summary>
    /// PUSH | SMS | EMAIL
    /// </summary>
    public string? NotificationMethod { get; set; }

    // ============================================================================
    // REMINDERS (nhắc nhở trước ca)
    // ============================================================================

    /// <summary>
    /// Nhắc trước 24h
    /// </summary>
    public bool Reminder24HSent { get; set; } = false;

    public DateTime? Reminder24HSentAt { get; set; }

    /// <summary>
    /// Nhắc trước 2h
    /// </summary>
    public bool Reminder2HSent { get; set; } = false;

    public DateTime? Reminder2HSentAt { get; set; }

    // ============================================================================
    // PERFORMANCE TRACKING (sau khi hoàn thành ca)
    // ============================================================================

    /// <summary>
    /// 0-5: Đánh giá đúng giờ
    /// </summary>
    public decimal? PunctualityScore { get; set; }

    /// <summary>
    /// Nhận xét của manager
    /// </summary>
    public string? PerformanceNote { get; set; }

    /// <summary>
    /// Manager đánh giá
    /// </summary>
    public Guid? RatedBy { get; set; }

    public DateTime? RatedAt { get; set; }

    // ============================================================================
    // NOTES
    // ============================================================================

    /// <summary>
    /// Ghi chú của manager khi assign
    /// </summary>
    public string? AssignmentNotes { get; set; }

    /// <summary>
    /// Phản hồi của guard sau ca
    /// </summary>
    public string? GuardNotes { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    /// <summary>
    /// Manager giao ca
    /// </summary>
    public Guid AssignedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Ca trực
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Shifts? Shift { get; set; }

    /// <summary>
    /// Team
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Teams? Team { get; set; }

    /// <summary>
    /// Guard được giao
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Guards? Guard { get; set; }

    /// <summary>
    /// Guard bị thay thế
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Guards? ReplacedGuard { get; set; }

    /// <summary>
    /// Manager giao ca
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Managers? AssignedByManager { get; set; }

    /// <summary>
    /// Manager đánh giá
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Managers? RatedByManager { get; set; }
}