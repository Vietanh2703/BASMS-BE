namespace Shifts.API.Extensions;

[Table("notification_logs")]
public class NotificationLog
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid ShiftId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid RecipientId { get; set; }
    public string RecipientType { get; set; } = "DIRECTOR";
    public string Action { get; set; } = "SHIFT_CREATED";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public string DeliveryMethod { get; set; } = "IN_APP";
    public string Status { get; set; } = "PENDING";
    public string Priority { get; set; } = "NORMAL";
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Write(false)]
    [Computed]
    public virtual Models.Shifts? Shift { get; set; }
}
