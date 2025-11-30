namespace Contracts.API.Models;

/// <summary>
/// ĐỊA ĐIỂM TRONG HỢP ĐỒNG
/// Link giữa Contract và Location - 1 hợp đồng có thể cover nhiều địa điểm
/// Ví dụ: Hợp đồng với siêu thị XYZ cover 5 chi nhánh
/// </summary>
[Table("contract_locations")]
public class ContractLocation
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Thuộc hợp đồng nào
    /// </summary>
    public Guid ContractId { get; set; }

    /// <summary>
    /// Địa điểm nào
    /// </summary>
    public Guid LocationId { get; set; }

    // ============================================================================
    // YÊU CẦU DỊCH VỤ TẠI ĐỊA ĐIỂM NÀY
    // ============================================================================

    /// <summary>
    /// Số lượng bảo vệ cần cho mỗi ca: 2, 3, 5...
    /// </summary>
    public int GuardsRequired { get; set; }

    /// <summary>
    /// Loại coverage: 24x7, day_only, night_only, weekdays_only, weekends_only, custom
    /// </summary>
    public string CoverageType { get; set; } = string.Empty;

    // ============================================================================
    // THỜI GIAN DỊCH VỤ
    // ============================================================================

    /// <summary>
    /// Ngày bắt đầu dịch vụ tại địa điểm này
    /// Có thể khác với ngày bắt đầu hợp đồng
    /// </summary>
    public DateTime ServiceStartDate { get; set; }

    /// <summary>
    /// Ngày kết thúc dịch vụ tại địa điểm này (optional)
    /// </summary>
    public DateTime? ServiceEndDate { get; set; }

    // ============================================================================
    // ƯU TIÊN
    // ============================================================================

    /// <summary>
    /// Có phải địa điểm chính không?
    /// Địa điểm chính ưu tiên cao hơn khi assign guards
    /// </summary>
    public bool IsPrimaryLocation { get; set; } = false;

    /// <summary>
    /// Mức độ ưu tiên: 1=cao nhất, 2=trung bình, 3=thấp
    /// Dùng khi assign guards nếu thiếu người
    /// </summary>
    public int PriorityLevel { get; set; } = 1;

    // ============================================================================
    // TỰ ĐỘNG TẠO CA
    // ============================================================================

    /// <summary>
    /// Tự động tạo shifts cho địa điểm này không?
    /// </summary>
    public bool AutoGenerateShifts { get; set; } = true;

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    // ============================================================================
    // METADATA
    // ============================================================================

    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
