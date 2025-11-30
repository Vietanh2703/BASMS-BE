using Dapper.Contrib.Extensions;

namespace Shifts.API.Models;

/// <summary>
/// USER_SYNC_LOG - Audit trail đồng bộ từ User Service
/// Chức năng: Track sync events, debug issues, monitor lag, audit history
/// Retention: 90 ngày (cleanup job monthly)
/// </summary>
[Table("user_sync_log")]
public class UserSyncLog
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // USER IDENTIFICATION
    // ============================================================================

    /// <summary>
    /// Manager/Guard ID được sync
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// MANAGER | GUARD
    /// </summary>
    public string UserType { get; set; } = string.Empty;

    // ============================================================================
    // CHI TIẾT SYNC
    // ============================================================================

    /// <summary>
    /// CREATE=user mới | UPDATE=thay đổi | DELETE=xóa | FULL_SYNC=sync toàn bộ
    /// </summary>
    public string SyncType { get; set; } = string.Empty;

    /// <summary>
    /// SUCCESS=OK | FAILED=lỗi | PARTIAL=1 số fields lỗi
    /// </summary>
    public string SyncStatus { get; set; } = string.Empty;

    // ============================================================================
    // CHANGES DETECTED
    // ============================================================================

    /// <summary>
    /// JSON array: ["phone_number", "employment_status"]
    /// </summary>
    public string? FieldsChanged { get; set; }

    /// <summary>
    /// JSON object giá trị cũ
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// JSON object giá trị mới
    /// </summary>
    public string? NewValues { get; set; }

    // ============================================================================
    // METADATA
    // ============================================================================

    /// <summary>
    /// WEBHOOK=realtime | SCHEDULED_JOB=backup | MANUAL=admin trigger | API_CALL
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

    // ============================================================================
    // ERROR INFO (nếu FAILED)
    // ============================================================================

    /// <summary>
    /// Chi tiết lỗi
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// USER_NOT_FOUND | NETWORK_TIMEOUT | VALIDATION_ERROR
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Số lần retry
    /// </summary>
    public int RetryCount { get; set; } = 0;

    // ============================================================================
    // TIMING (để tính sync lag)
    // ============================================================================

    /// <summary>
    /// Thời điểm bắt đầu sync
    /// </summary>
    public DateTime SyncStartedAt { get; set; }

    /// <summary>
    /// Thời điểm hoàn thành sync
    /// </summary>
    public DateTime? SyncCompletedAt { get; set; }

    /// <summary>
    /// Thời gian sync (ms) - alert nếu > 5000ms
    /// </summary>
    public int? SyncDurationMs { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
}
