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
    /// Loại tài liệu: contract, amendment, appendix, requirements, site_plan
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Danh mục hợp đồng: labor_contract, service_contract, etc.
    /// </summary>
    public string? Category { get; set; }

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
    /// Phiên bản tài liệu: 1.0, 1.1, 2.0...
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Token để truy cập tài liệu (nullable - chỉ có khi cần ký điện tử)
    /// </summary>
    public string? Tokens { get; set; }

    /// <summary>
    /// Ngày hết hạn token (nullable - NULL sau khi ký hoặc không cần token)
    /// </summary>
    public DateTime? TokenExpiredDay { get; set; }

    /// <summary>
    /// Ngày tài liệu
    /// </summary>
    public DateTime? DocumentDate { get; set; }

    /// <summary>
    /// Người upload
    /// </summary>
    public Guid? UploadedBy { get; set; }

    /// <summary>
    /// Email của khách hàng hoặc nhân viên (để gửi thông báo)
    /// </summary>
    public string? DocumentEmail { get; set; }

    /// <summary>
    /// Tên khách hàng hoặc nhân viên (để hiển thị trong email)
    /// </summary>
    public string? DocumentCustomerName { get; set; }

    /// <summary>
    /// Ngày bắt đầu hợp đồng (theo UTC+7 Vietnam)
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Ngày kết thúc hợp đồng (theo UTC+7 Vietnam)
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Ngày ký hợp đồng (theo UTC+7 Vietnam)
    /// </summary>
    public DateTime? SignDate { get; set; }

    /// <summary>
    /// Ngày Director/Manager approve hợp đồng (theo UTC+7 Vietnam)
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// User ID của người approve hợp đồng
    /// </summary>
    public Guid? ApprovedBy { get; set; }

    // ============================================================================
    // METADATA
    // ============================================================================

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
