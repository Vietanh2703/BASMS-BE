namespace Contracts.API.Models;

/// <summary>
/// LOG ĐỒNG BỘ CUSTOMER TỪ USERS SERVICE
/// Audit trail cho việc sync customer data
/// </summary>
[Table("customer_sync_log")]
public class CustomerSyncLog
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// User ID được sync
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Loại sync: CREATE | UPDATE | DELETE | ROLE_CHANGE
    /// </summary>
    public string SyncType { get; set; } = string.Empty;

    /// <summary>
    /// Trạng thái: SUCCESS | FAILED | PARTIAL
    /// </summary>
    public string SyncStatus { get; set; } = string.Empty;

    /// <summary>
    /// Fields được thay đổi (JSON array string)
    /// </summary>
    public string? FieldsChanged { get; set; }

    /// <summary>
    /// Giá trị cũ (JSON)
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// Giá trị mới (JSON)
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Sync được khởi tạo bởi: WEBHOOK | SCHEDULED_JOB | MANUAL | API_CALL
    /// </summary>
    public string? SyncInitiatedBy { get; set; }

    /// <summary>
    /// Version trước sync
    /// </summary>
    public int? UserServiceVersionBefore { get; set; }

    /// <summary>
    /// Version sau sync
    /// </summary>
    public int? UserServiceVersionAfter { get; set; }

    /// <summary>
    /// Error message nếu failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Số lần retry
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Thời điểm bắt đầu sync
    /// </summary>
    public DateTime SyncStartedAt { get; set; }

    /// <summary>
    /// Thời điểm hoàn thành sync
    /// </summary>
    public DateTime? SyncCompletedAt { get; set; }

    /// <summary>
    /// Thời gian sync (milliseconds)
    /// </summary>
    public int? SyncDurationMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
