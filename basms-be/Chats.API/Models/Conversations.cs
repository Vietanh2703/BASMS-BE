namespace Chats.API.Models;

/// <summary>
/// CONVERSATIONS - Cuộc trò chuyện/nhóm chat
/// Chức năng: Quản lý các cuộc hội thoại giữa users (1-1, group, team chat)
/// Use case: "Tạo group chat cho Team A", "Manager chat với Guard về ca trực"
/// </summary>
[Table("conversations")]
public class Conversations
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // LOẠI CUỘC TRÒ CHUYỆN
    // ============================================================================

    /// <summary>
    /// DIRECT=1-1 | GROUP=nhóm | TEAM=team chat | INCIDENT=chat về sự cố | SHIFT=chat về ca trực
    /// </summary>
    public string ConversationType { get; set; } = string.Empty;

    /// <summary>
    /// Tên cuộc trò chuyện (cho group/team)
    /// Ví dụ: "Team A - Ca đêm", "Xử lý sự cố INC001"
    /// NULL cho direct chat (tự động lấy tên từ participants)
    /// </summary>
    public string? ConversationName { get; set; }

    // ============================================================================
    // LIÊN KẾT ENTITY
    // ============================================================================

    /// <summary>
    /// Chat về ca trực cụ thể
    /// </summary>
    public Guid? ShiftId { get; set; }

    /// <summary>
    /// Chat về sự cố cụ thể
    /// </summary>
    public Guid? IncidentId { get; set; }

    /// <summary>
    /// Chat của team
    /// </summary>
    public Guid? TeamId { get; set; }

    /// <summary>
    /// Chat về hợp đồng
    /// </summary>
    public Guid? ContractId { get; set; }

    // ============================================================================
    // TRẠNG THÁI
    // ============================================================================

    /// <summary>
    /// Cuộc trò chuyện còn active
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ============================================================================
    // LAST MESSAGE TRACKING - CHO PREVIEW
    // ============================================================================

    /// <summary>
    /// Thời điểm tin nhắn cuối cùng
    /// </summary>
    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// Preview tin nhắn cuối (150 ký tự đầu)
    /// Ví dụ: "Manager: Ca ngày mai bạn có thể..."
    /// </summary>
    public string? LastMessagePreview { get; set; }

    /// <summary>
    /// User ID gửi tin nhắn cuối
    /// </summary>
    public Guid? LastMessageSenderId { get; set; }

    /// <summary>
    /// Tên người gửi tin nhắn cuối (cached)
    /// </summary>
    public string? LastMessageSenderName { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Người tạo cuộc trò chuyện
    /// </summary>
    public Guid? CreatedBy { get; set; }

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Tất cả tin nhắn trong cuộc trò chuyện
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<Messages> Messages { get; set; } = new List<Messages>();

    /// <summary>
    /// Danh sách người tham gia (qua junction table)
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<ConversationParticipants> Participants { get; set; } = new List<ConversationParticipants>();
}
