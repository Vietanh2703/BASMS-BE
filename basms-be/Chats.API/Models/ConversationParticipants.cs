namespace Chats.API.Models;

/// <summary>
/// CONVERSATION_PARTICIPANTS - Người tham gia cuộc trò chuyện
/// Chức năng: Junction table quản lý many-to-many giữa Conversations và Users
/// Use case: "Thêm Guard B vào group chat Team A", "User rời khỏi conversation"
/// </summary>
[Table("conversation_participants")]
public class ConversationParticipants
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // FOREIGN KEYS
    // ============================================================================

    /// <summary>
    /// Cuộc trò chuyện nào
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// User nào (từ Users.API)
    /// </summary>
    public Guid UserId { get; set; }

    // ============================================================================
    // USER INFO (CACHED)
    // ============================================================================

    /// <summary>
    /// Tên user (cached từ Users.API để hiển thị nhanh)
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Avatar URL (cached)
    /// </summary>
    public string? UserAvatarUrl { get; set; }

    /// <summary>
    /// Vai trò user: GUARD | MANAGER | ADMIN
    /// </summary>
    public string? UserRole { get; set; }

    // ============================================================================
    // PARTICIPANT ROLE IN CONVERSATION
    // ============================================================================

    /// <summary>
    /// Vai trò trong chat: MEMBER=thành viên | ADMIN=quản trị | OWNER=người tạo
    /// </summary>
    public string Role { get; set; } = "MEMBER";

    // ============================================================================
    // STATUS & TIMESTAMPS
    // ============================================================================

    /// <summary>
    /// Thời điểm tham gia
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Thời điểm rời (NULL=vẫn còn trong chat)
    /// </summary>
    public DateTime? LeftAt { get; set; }

    /// <summary>
    /// Còn active trong conversation
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ============================================================================
    // NOTIFICATION SETTINGS
    // ============================================================================

    /// <summary>
    /// Tắt thông báo cho conversation này
    /// </summary>
    public bool IsMuted { get; set; } = false;

    /// <summary>
    /// Thời điểm mute (NULL=không mute)
    /// </summary>
    public DateTime? MutedUntil { get; set; }

    // ============================================================================
    // READ TRACKING
    // ============================================================================

    /// <summary>
    /// Message ID cuối cùng đã đọc
    /// </summary>
    public Guid? LastReadMessageId { get; set; }

    /// <summary>
    /// Thời điểm đọc message cuối
    /// </summary>
    public DateTime? LastReadAt { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// User thêm participant này vào (NULL=tự tham gia)
    /// </summary>
    public Guid? AddedBy { get; set; }

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    [Write(false)]
    [Computed]
    public virtual Conversations? Conversation { get; set; }
}
