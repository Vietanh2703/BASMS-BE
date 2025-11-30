namespace Contracts.API.Models;

/// <summary>
/// KHÁCH HÀNG
/// Công ty/tổ chức thuê dịch vụ bảo vệ
/// Mỗi customer link với 1 user account từ User Service
/// </summary>
[Table("customers")]
public class Customer
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Link tới User ID từ User Service (1-1 relationship)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Mã khách hàng tự động: CUST-001, CUST-002...
    /// </summary>
    public string CustomerCode { get; set; } = string.Empty;

    /// <summary>
    /// Tên công ty khách hàng: "Bệnh viện ABC", "Siêu thị XYZ"
    /// </summary>
    public string CompanyName { get; set; } = string.Empty;

    // ============================================================================
    // THÔNG TIN LIÊN HỆ
    // ============================================================================

    /// <summary>
    /// Tên người đại diện liên hệ
    /// </summary>
    public string ContactPersonName { get; set; } = string.Empty;

    /// <summary>
    /// Chức danh: "Giám đốc hành chính", "Trưởng phòng hành chính"
    /// </summary>
    public string? ContactPersonTitle { get; set; }
    
    /// <summary>
    /// Số CCCD
    /// </summary>
    public string IdentityNumber { get; set; } = string.Empty;

    /// <summary>
    /// Ngày cấp CCCD
    /// </summary>
    public DateTime? IdentityIssueDate { get; set; }

    /// <summary>
    /// Nơi cấp CCCD
    /// </summary>
    public string? IdentityIssuePlace { get; set; }

    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    
    public string AvatarUrl { get; set; }
    public string? Gender { get; set; }
    public DateTime DateOfBirth { get; set; }

    // ============================================================================
    // ĐỊA CHỈ
    // ============================================================================

    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? District { get; set; }

    // ============================================================================
    // PHÂN LOẠI KHÁCH HÀNG
    // ============================================================================

    /// <summary>
    /// Ngành nghề: retail, office, manufacturing, hospital, school, residential
    /// </summary>
    public string? Industry { get; set; }

    /// <summary>
    /// Quy mô: small, medium, large, enterprise
    /// </summary>
    public string? CompanySize { get; set; }

    // ============================================================================
    // TRẠNG THÁI
    // ============================================================================

    /// <summary>
    /// Ngày bắt đầu là khách hàng
    /// </summary>
    public DateTime CustomerSince { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Trạng thái: active, inactive, suspended
    /// </summary>
    public string Status { get; set; } = "active";

    // ============================================================================
    // CÀI ĐẶT LỊCH LÀM VIỆC
    // ============================================================================

    /// <summary>
    /// Có theo ngày lễ Việt Nam không?
    /// true = nghỉ theo lịch lễ quốc gia
    /// </summary>
    public bool FollowsNationalHolidays { get; set; } = true;

    /// <summary>
    /// Ghi chú về khách hàng
    /// </summary>
    public string? Notes { get; set; }

    // ============================================================================
    // METADATA
    // ============================================================================

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
