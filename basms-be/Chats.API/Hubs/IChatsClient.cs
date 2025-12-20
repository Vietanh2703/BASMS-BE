namespace Chats.API.Hubs;

/// <summary>
/// Typed client interface for SignalR
/// Định nghĩa các methods mà server có thể gọi trên client
/// </summary>
public interface IChatsClient
{
    // ============================================================================
    // MESSAGES
    // ============================================================================

    /// <summary>
    /// Client nhận tin nhắn mới
    /// </summary>
    Task ReceiveMessage(MessageDto message);

    /// <summary>
    /// Tin nhắn đã được chỉnh sửa
    /// </summary>
    Task MessageEdited(Guid messageId, string newContent, DateTime editedAt);

    /// <summary>
    /// Tin nhắn đã bị xóa
    /// </summary>
    Task MessageDeleted(Guid messageId, Guid deletedBy, DateTime deletedAt);

    // ============================================================================
    // PRESENCE - Online/Offline Status
    // ============================================================================

    /// <summary>
    /// User đã online
    /// </summary>
    Task UserOnline(string userId);

    /// <summary>
    /// User đã offline
    /// </summary>
    Task UserOffline(string userId, DateTime lastSeen);

    // ============================================================================
    // TYPING INDICATORS
    // ============================================================================

    /// <summary>
    /// User đang gõ tin nhắn
    /// </summary>
    Task UserIsTyping(string userId, string conversationId);

    /// <summary>
    /// User đã dừng gõ tin nhắn
    /// </summary>
    Task UserStoppedTyping(string userId, string conversationId);

    // ============================================================================
    // READ RECEIPTS
    // ============================================================================

    /// <summary>
    /// Tin nhắn đã được đọc bởi user
    /// </summary>
    Task MessageRead(Guid messageId, string userId, DateTime readAt);

    // ============================================================================
    // REACTIONS (Phase 6)
    // ============================================================================

    /// <summary>
    /// User đã react vào tin nhắn
    /// </summary>
    Task MessageReacted(Guid messageId, string userId, string reaction);

    /// <summary>
    /// User đã bỏ reaction
    /// </summary>
    Task MessageReactionRemoved(Guid messageId, string userId);

    // ============================================================================
    // CONVERSATION EVENTS
    // ============================================================================

    /// <summary>
    /// Conversation mới được tạo
    /// </summary>
    Task ConversationCreated(Guid conversationId, string conversationType);

    /// <summary>
    /// User được thêm vào conversation
    /// </summary>
    Task ParticipantAdded(Guid conversationId, string userId, string userName);

    /// <summary>
    /// User bị remove khỏi conversation
    /// </summary>
    Task ParticipantRemoved(Guid conversationId, string userId);

    /// <summary>
    /// Conversation info được update (name, avatar, etc.)
    /// </summary>
    Task ConversationUpdated(Guid conversationId, string updatedField, string newValue);
}

/// <summary>
/// DTO cho message (sử dụng trong SignalR)
/// </summary>
public record MessageDto
{
    public Guid Id { get; init; }
    public Guid ConversationId { get; init; }
    public Guid SenderId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string? SenderAvatarUrl { get; init; }
    public string MessageType { get; init; } = "TEXT";
    public string? Content { get; init; }
    public string? FileUrl { get; init; }
    public string? FileName { get; init; }
    public long? FileSize { get; init; }
    public string? FileType { get; init; }
    public string? ThumbnailUrl { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public string? LocationAddress { get; init; }
    public string? LocationMapUrl { get; init; }
    public Guid? ReplyToMessageId { get; init; }
    public bool IsEdited { get; init; }
    public DateTime? EditedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
