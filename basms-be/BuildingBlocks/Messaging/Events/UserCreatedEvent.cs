namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// Event published when a new user is created in Users Service
/// Consumed by: Shifts Service (to create manager/guard cache)
/// </summary>
public record UserCreatedEvent
{
    public Guid UserId { get; init; }
    public string FirebaseUid { get; init; } = string.Empty;
    public string IdentityNumber { get; set; }
    public DateTime? IdentityIssueDate { get; init; }
    public string? IdentityIssuePlace { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? AvatarUrl { get; init; }
    
    // Role information
    public Guid RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty; // "admin", "manager", "director", "guard", "customer"

    // Additional info for managers
    public string? EmployeeCode { get; init; }
    public string? Position { get; init; }
    public string? Department { get; init; }

    // Additional info for guards
    public DateTime DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? Address { get; init; }
    public DateTime? HireDate { get; init; }
    public string? ContractType { get; init; }
    public string? CertificationLevel { get; init; } // Hạng chứng chỉ: I, II, III, IV, V, VI
    public decimal? StandardWage { get; init; } // Mức lương cơ bản (VNĐ/tháng)

    // Metadata
    public DateTime CreatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
    public int Version { get; init; } = 1; // For sync tracking
}