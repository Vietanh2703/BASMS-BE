namespace Users.API.Models;

[Table("audit_logs")]
public class AuditLogs
{
    [ExplicitKey]
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? DeviceId { get; set; }

    public string Status { get; set; } = "success";

    public string? ErrorMessage { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
    
    [Write(false)]
    [Computed]
    public virtual Users? User { get; set; }
}
