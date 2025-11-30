using Dapper.Contrib.Extensions;

namespace Users.API.Models;

[Table("password_reset_tokens")]
public class PasswordResetTokens
{
    [ExplicitKey]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty; // Store hashed new password temporarily

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public DateTime? UsedAt { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // Navigation property - ignored by Dapper
    [Write(false)]
    [Computed]
    public virtual Users? User { get; set; }
}

