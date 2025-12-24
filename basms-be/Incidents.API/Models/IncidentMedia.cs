namespace Incidents.API.Models;

/// <summary>
/// INCIDENT_MEDIA - Media files đính kèm sự cố
/// Chức năng: Lưu trữ ảnh, video bằng chứng của sự cố
/// Use case: "Guard chụp ảnh hiện trường, video camera an ninh"
/// </summary>
[Table("incident_media")]
public class IncidentMedia
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // FOREIGN KEY
    // ============================================================================

    /// <summary>
    /// Sự cố nào
    /// </summary>
    public Guid IncidentId { get; set; }

    // ============================================================================
    // THÔNG TIN FILE
    // ============================================================================

    /// <summary>
    /// Loại media: IMAGE=ảnh | VIDEO=video | AUDIO=âm thanh | DOCUMENT=tài liệu
    /// </summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// URL file trên S3/storage
    /// Ví dụ: "s3://basms/incidents/INC001/photo1.jpg"
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// Tên file gốc
    /// Ví dụ: "evidence_photo_01.jpg"
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Kích thước file (bytes)
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// MIME type: image/jpeg, video/mp4, etc.
    /// </summary>
    public string? MimeType { get; set; }

    // ============================================================================
    // PREVIEW & DISPLAY
    // ============================================================================

    /// <summary>
    /// URL thumbnail cho preview nhanh
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Chú thích/mô tả cho file
    /// Ví dụ: "Ảnh cổng chính lúc 23:00"
    /// </summary>
    public string? Caption { get; set; }

    /// <summary>
    /// Thứ tự hiển thị (để sắp xếp media)
    /// </summary>
    public int? DisplayOrder { get; set; }

    // ============================================================================
    // UPLOADED BY
    // ============================================================================

    /// <summary>
    /// User upload file (thường là reporter)
    /// </summary>
    public Guid? UploadedBy { get; set; }

    /// <summary>
    /// Tên user upload (cached)
    /// </summary>
    public string? UploadedByName { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Sự cố liên quan
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Incidents? Incident { get; set; }
}
