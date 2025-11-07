namespace Contracts.API.Models;

/// <summary>
/// HỒ SƠ CÔNG TY BẢO VỆ
/// Chứa thông tin về công ty cung cấp dịch vụ bảo vệ
/// Thường chỉ có 1 record trong database (công ty của mình)
/// </summary>
[Table("company_profile")]
public class CompanyProfile
{
    /// <summary>
    /// ID duy nhất của công ty
    /// </summary>
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Tên công ty: "Công ty TNHH An ninh XYZ"
    /// </summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>
    /// Giấy phép kinh doanh (duy nhất)
    /// </summary>
    public string BusinessLicense { get; set; } = string.Empty;

    /// <summary>
    /// Mã số thuế (duy nhất)
    /// </summary>
    public string TaxCode { get; set; } = string.Empty;

    /// <summary>
    /// Địa chỉ trụ sở chính
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Số điện thoại liên hệ
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Email công ty
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Website công ty (optional)
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// URL logo công ty
    /// </summary>
    public string? LogoUrl { get; set; }

    // ============================================================================
    // CÀI ĐẶT LỊCH LÀM VIỆC MẶC ĐỊNH
    // ============================================================================

    /// <summary>
    /// Ngày bắt đầu tuần làm việc mặc định
    /// 1 = Thứ 2, 2 = Thứ 3, ..., 7 = Chủ nhật
    /// </summary>
    public int DefaultWorkWeekStart { get; set; } = 1; // Thứ 2

    /// <summary>
    /// Các ngày cuối tuần mặc định (lưu dạng chuỗi cách nhau bởi dấu phẩy)
    /// Ví dụ: "6,7" = Thứ 7 và Chủ nhật
    /// </summary>
    public string DefaultWeekendDays { get; set; } = "6,7";

    // ============================================================================
    // METADATA
    // ============================================================================

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
