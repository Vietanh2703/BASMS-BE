namespace Chats.API.Models;

/// <summary>
/// MESSAGE_READ_RECEIPTS - Người đã đọc tin nhắn
/// Chức năng: Junction table theo dõi ai đã đọc tin nhắn nào, khi nào
/// Use case: "Manager thấy Guard đã đọc tin nhắn lúc 14:30", "Blue double check"
/// </summary>
[Table("message_read_receipts")]
public class MessageReadReceipts
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // FOREIGN KEYS
    // ============================================================================

    /// <summary>
    /// Tin nhắn nào
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// User nào đã đọc (từ Users.API)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Conversation ID (denormalized để query nhanh)
    /// </summary>
    public Guid ConversationId { get; set; }

    // ============================================================================
    // USER INFO (CACHED)
    // ============================================================================

    /// <summary>
    /// Tên user (cached)
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Avatar user (cached)
    /// </summary>
    public string? UserAvatarUrl { get; set; }

    // ============================================================================
    // READ TRACKING
    // ============================================================================

    /// <summary>
    /// Thời điểm đọc tin nhắn
    /// </summary>
    public DateTime ReadAt { get; set; }

    /// <summary>
    /// Đã đọc từ thiết bị nào: MOBILE | WEB | DESKTOP
    /// </summary>
    public string? ReadFrom { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    [Write(false)]
    [Computed]
    public virtual Messages? Message { get; set; }
}
