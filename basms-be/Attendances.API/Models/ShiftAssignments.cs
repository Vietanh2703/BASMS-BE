using Dapper.Contrib.Extensions;

namespace Attendances.API.Models;

/// <summary>
/// SHIFT_ASSIGNMENTS - Cache thông tin phân công ca từ Shifts Service
/// Mục đích: Reference data cho attendance records
/// </summary>
[Table("shift_assignments")]
public class ShiftAssignments
{
    [ExplicitKey]
    public Guid Id { get; set; }

    public Guid ShiftId { get; set; }
    public Guid GuardId { get; set; }

    /// <summary>
    /// PENDING | CONFIRMED | DECLINED | COMPLETED | NO_SHOW | CANCELLED
    /// </summary>
    public string Status { get; set; } = "PENDING";

    public DateTime AssignedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public bool IsAttended { get; set; } = false;
    public bool IsNoShow { get; set; } = false;

    // ============================================================================
    // SYNC METADATA
    // ============================================================================

    public DateTime? LastSyncedAt { get; set; }
    public string SyncStatus { get; set; } = "SYNCED";

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

}
