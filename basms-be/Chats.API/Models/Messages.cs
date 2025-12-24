namespace Chats.API.Models;

/// <summary>
/// MESSAGES - Tin nhắn trong cuộc trò chuyện
/// Chức năng: Lưu trữ tin nhắn text, file, location, reply, read receipts
/// Use case: "Guard gửi tin nhắn cho Manager về tình hình ca trực"
/// </summary>
[Table("messages")]
public class Messages
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

    // ============================================================================
    // NGƯỜI GỬI
    // ============================================================================

    /// <summary>
    /// ID người gửi (user_id từ Users.API)
    /// </summary>
    public Guid SenderId { get; set; }

    /// <summary>
    /// Tên người gửi (cached để hiển thị nhanh)
    /// </summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// Avatar người gửi (cached)
    /// </summary>
    public string? SenderAvatarUrl { get; set; }

    // ============================================================================
    // NỘI DUNG TIN NHẮN
    // ============================================================================

    /// <summary>
    /// Loại tin nhắn: TEXT=văn bản | IMAGE=ảnh | VIDEO=video |
    /// FILE=file đính kèm | AUDIO=voice message | LOCATION=vị trí | SYSTEM=tin nhắn hệ thống
    /// </summary>
    public string MessageType { get; set; } = "TEXT";

    /// <summary>
    /// Nội dung text
    /// </summary>
    public string? Content { get; set; }

    // ============================================================================
    // FILE ĐÍNH KÈM (cho IMAGE, VIDEO, FILE, AUDIO)
    // ============================================================================

    /// <summary>
    /// URL file trên S3/storage
    /// </summary>
    public string? FileUrl { get; set; }

    /// <summary>
    /// Tên file gốc
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Kích thước file (bytes)
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// MIME type: image/jpeg, video/mp4, application/pdf, etc.
    /// </summary>
    public string? FileType { get; set; }

    /// <summary>
    /// URL thumbnail (cho ảnh/video)
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    // ============================================================================
    // LOCATION SHARING
    // ============================================================================

    /// <summary>
    /// Vĩ độ (cho LOCATION type)
    /// </summary>
    public decimal? Latitude { get; set; }

    /// <summary>
    /// Kinh độ (cho LOCATION type)
    /// </summary>
    public decimal? Longitude { get; set; }

    /// <summary>
    /// Địa chỉ readable "123 Đường ABC, Quận 1"
    /// </summary>
    public string? LocationAddress { get; set; }

    /// <summary>
    /// URL static map preview (Google Maps, Mapbox, etc.)
    /// </summary>
    public string? LocationMapUrl { get; set; }

    // ============================================================================
    // REPLY/THREAD
    // ============================================================================

    /// <summary>
    /// Trả lời tin nhắn nào (NULL=tin nhắn gốc)
    /// </summary>
    public Guid? ReplyToMessageId { get; set; }

    // ============================================================================
    // EDIT & DELETE
    // ============================================================================

    /// <summary>
    /// Tin nhắn đã được chỉnh sửa
    /// </summary>
    public bool IsEdited { get; set; } = false;

    /// <summary>
    /// Thời điểm chỉnh sửa
    /// </summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>
    /// Tin nhắn đã xóa (soft delete)
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Thời điểm xóa
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// User xóa tin nhắn (có thể khác sender)
    /// </summary>
    public Guid? DeletedBy { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Cuộc trò chuyện
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Conversations? Conversation { get; set; }

    /// <summary>
    /// Tin nhắn được reply (nếu có)
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Messages? ReplyToMessage { get; set; }

    /// <summary>
    /// Các tin nhắn reply tin nhắn này
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<Messages> Replies { get; set; } = new List<Messages>();

    /// <summary>
    /// Danh sách user đã đọc (qua junction table)
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<MessageReadReceipts> ReadReceipts { get; set; } = new List<MessageReadReceipts>();
}
