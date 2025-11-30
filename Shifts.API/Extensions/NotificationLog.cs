namespace Shifts.API.Extensions;

/// <summary>
/// NOTIFICATION_LOG - Lưu lịch sử gửi thông báo cho director và customer
/// Chức năng: Track tất cả thông báo về shift create/update/cancel
/// Use case: "Thông báo cho director và customer khi có shift mới tạo"
/// </summary>
[Table("notification_logs")]
public class NotificationLog
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // FOREIGN KEYS
    // ============================================================================

    /// <summary>
    /// Shift liên quan
    /// </summary>
    public Guid ShiftId { get; set; }

    /// <summary>
    /// Contract liên quan
    /// </summary>
    public Guid? ContractId { get; set; }

    /// <summary>
    /// User nhận thông báo (Director hoặc Customer)
    /// </summary>
    public Guid RecipientId { get; set; }

    // ============================================================================
    // NOTIFICATION INFO
    // ============================================================================

    /// <summary>
    /// Loại người nhận: DIRECTOR | CUSTOMER | MANAGER
    /// </summary>
    public string RecipientType { get; set; } = "DIRECTOR";

    /// <summary>
    /// Hành động: SHIFT_CREATED | SHIFT_UPDATED | SHIFT_CANCELLED | SHIFT_APPROVED
    /// </summary>
    public string Action { get; set; } = "SHIFT_CREATED";

    /// <summary>
    /// Tiêu đề thông báo
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Nội dung thông báo
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Dữ liệu bổ sung (JSON): shift info, location info, etc.
    /// </summary>
    public string? Metadata { get; set; }

    // ============================================================================
    // DELIVERY INFO
    // ============================================================================

    /// <summary>
    /// Phương thức gửi: EMAIL | SMS | PUSH | IN_APP
    /// </summary>
    public string DeliveryMethod { get; set; } = "IN_APP";

    /// <summary>
    /// Trạng thái gửi: PENDING | SENT | FAILED | DELIVERED | READ
    /// </summary>
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// Thời gian gửi
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Thời gian đọc
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Đã đọc hay chưa
    /// </summary>
    public bool IsRead { get; set; } = false;

    // ============================================================================
    // ERROR TRACKING
    // ============================================================================

    /// <summary>
    /// Số lần thử gửi lại
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Lỗi khi gửi
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Thời gian thử gửi lần cuối
    /// </summary>
    public DateTime? LastRetryAt { get; set; }

    // ============================================================================
    // PRIORITY & EXPIRY
    // ============================================================================

    /// <summary>
    /// Mức độ ưu tiên: LOW | NORMAL | HIGH | URGENT
    /// </summary>
    public string Priority { get; set; } = "NORMAL";

    /// <summary>
    /// Thời gian hết hạn (notification không còn liên quan)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Đã hết hạn
    /// </summary>
    public bool IsExpired { get; set; } = false;

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    [Write(false)]
    [Computed]
    public virtual Models.Shifts? Shift { get; set; }
}
