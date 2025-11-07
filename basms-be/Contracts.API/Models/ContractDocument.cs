namespace Contracts.API.Models;

/// <summary>
/// TÀI LIỆU HỢP ĐỒNG
/// Lưu trữ các file liên quan đến hợp đồng
/// Ví dụ: PDF hợp đồng gốc, phụ lục, sơ đồ địa điểm, yêu cầu đặc biệt...
/// </summary>
[Table("contract_documents")]
public class ContractDocument
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Thuộc hợp đồng nào
    /// </summary>
    public Guid ContractId { get; set; }

    /// <summary>
    /// Loại tài liệu: contract, amendment, appendix, requirements, site_plan
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Tên tài liệu: "Hợp đồng dịch vụ bảo vệ - signed.pdf"
    /// </summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// URL file trên storage (S3, Azure Blob...)
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// Kích thước file (bytes)
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// MIME type: application/pdf, image/jpeg...
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Phiên bản tài liệu: 1.0, 1.1, 2.0...
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Ngày tài liệu
    /// </summary>
    public DateTime? DocumentDate { get; set; }

    /// <summary>
    /// Người upload
    /// </summary>
    public Guid? UploadedBy { get; set; }

    // ============================================================================
    // METADATA
    // ============================================================================

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
