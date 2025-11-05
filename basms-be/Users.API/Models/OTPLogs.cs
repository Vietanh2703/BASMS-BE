using Dapper.Contrib.Extensions;

namespace Users.API.Models;

[Table("otp_logs")]
public class OTPLogs
{
    [ExplicitKey]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string OtpCode { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty; // login, verify_email, reset_password, etc.

    public string DeliveryMethod { get; set; } = "email"; // email, sms

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public DateTime? UsedAt { get; set; }

    public int AttemptCount { get; set; } = 0;

    public DateTime? LastAttemptAt { get; set; }

    public bool IsExpired { get; set; } = false;

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
