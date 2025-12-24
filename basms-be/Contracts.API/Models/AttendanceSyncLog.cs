namespace Contracts.API.Models;

[Table("attendance_sync_log")]
public class AttendanceSyncLog
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public DateTime SyncDate { get; set; }
    public int? TotalShifts { get; set; }
    public int? TotalCheckIns { get; set; }
    public int? TotalCheckOuts { get; set; }
    public string SyncStatus { get; set; } = "pending";
    public DateTime? SyncedAt { get; set; }
    public string? Notes { get; set; }
}
