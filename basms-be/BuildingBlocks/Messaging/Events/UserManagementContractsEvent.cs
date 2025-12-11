namespace BuildingBlocks.Messaging.Events;

// ================================================================
// CREATE USER CONTRACTS
// ================================================================

/// <summary>
/// Request để tạo user mới qua MassTransit
/// Used by Contracts.API to create customer/guard account in Users.API
/// </summary>
public record CreateUserRequest
{
    public string? IdentityNumber { get; init; }
    public DateTime? IdentityIssueDate { get; init; }
    public string? IdentityIssuePlace { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool EmailVerified { get; init; } = false;
    public string Password { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public bool PhoneVerified { get; init; } = false;
    public string? Address { get; init; }
    public string? Gender { get; init; }
    public int? BirthDay { get; init; }
    public int? BirthMonth { get; init; }
    public int? BirthYear { get; init; }
    public string RoleName { get; init; } = "customer";  // Default role: customer, guard, manager, admin
    public string? AvatarUrl { get; init; }
    public string AuthProvider { get; init; } = "email";  // email, google, facebook
    public string Status { get; init; } = "active";
    public int LoginCount { get; init; } = 0;
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

// ================================================================
// UPDATE GUARD INFO EVENT
// ================================================================

/// <summary>
/// Event để update thông tin Guard (CertificationLevel, StandardWage)
/// Published by Contracts.API sau khi import working contract
/// Consumed by Shifts.API để update Guard record
/// </summary>
public record UpdateGuardInfoEvent
{
    public Guid GuardId { get; init; }
    public string? Email { get; init; }
    public string? CertificationLevel { get; init; } // Hạng chứng chỉ: I, II, III, IV, V, VI
    public decimal? StandardWage { get; init; } // Mức lương cơ bản (VNĐ/tháng)
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}

// ================================================================
// UPDATE MANAGER INFO EVENT
// ================================================================

/// <summary>
/// Event để update thông tin Manager (CertificationLevel, StandardWage, TotalGuardsSupervised)
/// Published by Contracts.API sau khi import manager working contract
/// Consumed by Shifts.API để update Manager record
/// </summary>
public record UpdateManagerInfoEvent
{
    public Guid ManagerId { get; init; }
    public string? Email { get; init; }
    public string? CertificationLevel { get; init; } // Hạng chứng chỉ: I, II, III, IV, V, VI
    public decimal? StandardWage { get; init; } // Mức lương cơ bản (VNĐ/tháng)
    public int? TotalGuardsSupervised { get; init; } // Tổng số guards được phân công quản lý
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}

// ================================================================
// DEACTIVATE USER EVENT
// ================================================================

/// <summary>
/// Event để deactivate user khi contract hết hạn
/// Published by Contracts.API
/// Consumed by Users.API
/// </summary>
public record DeactivateUserEvent
{
    public string Email { get; init; } = string.Empty;
    public string UserType { get; init; } = string.Empty; // "manager", "guard", "customer"
    public string Reason { get; init; } = string.Empty; // "Contract expired"
    public DateTime DeactivatedAt { get; init; } = DateTime.UtcNow;
}

// ================================================================
// DEACTIVATE MANAGER/GUARD EVENTS
// ================================================================

/// <summary>
/// Event để deactivate Manager khi contract hết hạn
/// Published by Contracts.API
/// Consumed by Shifts.API
/// </summary>
public record DeactivateManagerEvent
{
    public Guid ManagerId { get; init; }
    public string? Email { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime DeactivatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event để deactivate Guard khi contract hết hạn
/// Published by Contracts.API
/// Consumed by Shifts.API
/// </summary>
public record DeactivateGuardEvent
{
    public Guid GuardId { get; init; }
    public string? Email { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime DeactivatedAt { get; init; } = DateTime.UtcNow;
}
