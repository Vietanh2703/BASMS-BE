namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// Event published when a user is deleted (soft delete) in Users Service
/// Consumed by: Shifts Service (to mark manager/guard as deleted)
/// </summary>
public record UserDeletedEvent
{
    public Guid UserId { get; init; }
    public string RoleName { get; init; } = string.Empty;
    public DateTime DeletedAt { get; init; }
    public Guid? DeletedBy { get; init; }
    public string? DeletionReason { get; init; }
    public bool IsSoftDelete { get; init; } = true;
}