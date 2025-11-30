namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// Event published when a user's role is changed
/// Example: Guard promoted to Manager, or Manager demoted to Guard
/// Consumed by: Shifts Service (to migrate data between managers/guards tables)
/// </summary>
public record UserRoleChangedEvent
{
    public Guid UserId { get; init; }
    public string OldRoleName { get; init; } = string.Empty;
    public string NewRoleName { get; init; } = string.Empty;
    public Guid OldRoleId { get; init; }
    public Guid NewRoleId { get; init; }

    // User info
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? EmployeeCode { get; init; }

    // Metadata
    public DateTime ChangedAt { get; init; }
    public Guid? ChangedBy { get; init; }
    public string? ChangeReason { get; init; }
    public int Version { get; init; }
}