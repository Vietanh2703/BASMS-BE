namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// Event published when a user is updated in Users Service
/// Consumed by: Shifts Service (to update manager/guard cache)
/// </summary>
public record UserUpdatedEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? AvatarUrl { get; init; }

    // Role information (might have changed)
    public Guid RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;

    // Additional info
    public string? EmployeeCode { get; init; }
    public string? Position { get; init; }
    public string? Department { get; init; }
    public string? Address { get; init; }
    public string Status { get; init; } = "active"; // active, inactive, suspended

    // For guards
    public string? ContractType { get; init; }
    public DateTime? TerminationDate { get; init; }
    public string? TerminationReason { get; init; }

    // Metadata
    public DateTime UpdatedAt { get; init; }
    public Guid? UpdatedBy { get; init; }
    public int Version { get; init; } // Incremented version for optimistic locking

    // Changed fields tracking
    public List<string> ChangedFields { get; init; } = new();
}