namespace Contracts.API.Models;

[Table("customer_sync_log")]
public class CustomerSyncLog
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SyncType { get; set; } = string.Empty;
    public string SyncStatus { get; set; } = string.Empty;
    public string? FieldsChanged { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? SyncInitiatedBy { get; set; }
    public int? UserServiceVersionAfter { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime SyncStartedAt { get; set; }
    public DateTime? SyncCompletedAt { get; set; }
    public int? SyncDurationMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
