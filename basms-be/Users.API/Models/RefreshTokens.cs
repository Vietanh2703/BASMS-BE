namespace Users.API.Models;

[Table("refresh_tokens")]
public class RefreshTokens
{
    [ExplicitKey]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty;

    public string? JwtId { get; set; } // Link to access token

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public bool IsRevoked { get; set; } = false;

    public DateTime? RevokedAt { get; set; }

    public string? ReplacedByToken { get; set; } // Token rotation

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