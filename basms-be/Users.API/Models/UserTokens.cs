namespace Users.API.Models;

[Table("user_tokens")]
public class UserTokens
{
    [ExplicitKey]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty;

    public string TokenType { get; set; } = string.Empty; // access, refresh, reset_password, email_verification

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; } = false;

    public DateTime? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? DeviceId { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // Navigation property - ignored by Dapper
    [Write(false)]
    [Computed]
    public virtual Users? User { get; set; }
}