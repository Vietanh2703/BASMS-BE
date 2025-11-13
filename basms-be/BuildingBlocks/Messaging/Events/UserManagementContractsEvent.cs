namespace BuildingBlocks.Messaging.Events;

// ================================================================
// CREATE USER CONTRACTS
// ================================================================

/// <summary>
/// Request để tạo user mới qua MassTransit
/// Used by Contracts.API to create customer account in Users.API
/// </summary>
public record CreateUserRequest
{
    public string? IdentityNumber { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string Gender { get; init; }
    public string RoleName { get; init; } = "customer";  // Default role
    public string? AvatarUrl { get; init; }
    public string AuthProvider { get; init; } = "email";
}

/// <summary>
/// Response từ Users.API sau khi tạo user thành công
/// </summary>
public record CreateUserResponse
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    // User information
    public Guid UserId { get; init; }
    public string? FirebaseUid { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;

    // For email sending
    public string GeneratedPassword { get; init; } = string.Empty;
}

// ================================================================
// GET USER BY EMAIL CONTRACTS
// ================================================================

/// <summary>
/// Request để lấy user theo email
/// </summary>
public record GetUserByEmailRequest
{
    public string Email { get; init; } = string.Empty;
}

/// <summary>
/// Response với thông tin user
/// </summary>
public record GetUserByEmailResponse
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public Guid? UserId { get; init; }
    public string? Email { get; init; }
    public string? FullName { get; init; }
    public string? Phone { get; init; }
    public bool UserExists { get; init; }
}
