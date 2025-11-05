namespace Users.API.Models;

[Table("user_sessions")]
public class UserSessions
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime ExpiresAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}