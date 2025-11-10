namespace Contracts.API.Models;

/// <summary>
/// ĐỊA ĐIỂM KHÁCH HÀNG
/// Các địa điểm vật lý nơi bảo vệ được triển khai
/// Ví dụ: Chi nhánh A, Nhà máy B, Kho C...
/// </summary>
[Table("customer_locations")]
public class CustomerLocation
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Thuộc khách hàng nào
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Mã địa điểm: LOC-001, LOC-002 (unique per customer)
    /// </summary>
    public string LocationCode { get; set; } = string.Empty;

    /// <summary>
    /// Tên địa điểm: "Chi nhánh Quận 1", "Nhà máy Long An"
    /// </summary>
    public string LocationName { get; set; } = string.Empty;

    /// <summary>
    /// Loại địa điểm: office, warehouse, factory, retail_store, residential, hospital, industrial
    /// Quan trọng để xác định requirements về bảo vệ
    /// </summary>
    public string LocationType { get; set; } = string.Empty;

    // ============================================================================
    // ĐỊA CHỈ
    // ============================================================================

    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }

    // ============================================================================
    // GEOFENCING - Cho Attendance Service check-in/out
    // ============================================================================

    /// <summary>
    /// Vĩ độ: 10.762622
    /// </summary>
    public decimal? Latitude { get; set; }

    /// <summary>
    /// Kinh độ: 106.660172
    /// </summary>
    public decimal? Longitude { get; set; }

    /// <summary>
    /// Bán kính check-in (meters): 100m mặc định
    /// Guards phải ở trong phạm vi này mới check-in được
    /// </summary>
    public int GeofenceRadiusMeters { get; set; } = 100;

    /// <summary>
    /// Độ cao (meters) - Optional, dùng cho buildings cao tầng
    /// </summary>
    public decimal? AltitudeMeters { get; set; }

    // ============================================================================
    // LIÊN HỆ TẠI CHỖ
    // ============================================================================

    /// <summary>
    /// Tên quản lý địa điểm
    /// </summary>
    public string? SiteManagerName { get; set; }

    /// <summary>
    /// SĐT quản lý địa điểm
    /// </summary>
    public string? SiteManagerPhone { get; set; }

    /// <summary>
    /// Liên hệ khẩn cấp
    /// </summary>
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    // ============================================================================
    // ĐẶC ĐIỂM HOẠT ĐỘNG
    // ============================================================================

    /// <summary>
    /// Loại giờ hoạt động: 24/7, business_hours, shift_based, seasonal
    /// </summary>
    public string OperatingHoursType { get; set; } = "24/7";

    /// <summary>
    /// Diện tích (m2)
    /// </summary>
    public decimal? TotalAreaSqm { get; set; }

    /// <summary>
    /// Số tầng
    /// </summary>
    public int? BuildingFloors { get; set; }

    // ============================================================================
    // LỊCH LÀM VIỆC CỦA ĐỊA ĐIỂM
    // ============================================================================

    /// <summary>
    /// Có theo lịch làm việc chuẩn không? (T2-T6 làm, T7-CN nghỉ)
    /// </summary>
    public bool FollowsStandardWorkweek { get; set; } = true;

    /// <summary>
    /// Ngày cuối tuần tùy chỉnh (nếu không theo chuẩn)
    /// Ví dụ: "7" = chỉ nghỉ Chủ nhật
    /// </summary>
    public string? CustomWeekendDays { get; set; }

    // ============================================================================
    // YÊU CẦU ĐẶC BIỆT VỀ BẢO VỆ
    // ============================================================================

    /// <summary>
    /// Yêu cầu bảo vệ 24/7 liên tục
    /// </summary>
    public bool Requires24x7Coverage { get; set; } = false;

    /// <summary>
    /// Cho phép 1 bảo vệ đơn lẻ hay bắt buộc tối thiểu 2?
    /// </summary>
    public bool AllowsSingleGuard { get; set; } = true;

    /// <summary>
    /// Số lượng bảo vệ tối thiểu yêu cầu
    /// </summary>
    public int MinimumGuardsRequired { get; set; } = 1;

    // ============================================================================
    // TRẠNG THÁI
    // ============================================================================

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;

    // ============================================================================
    // METADATA
    // ============================================================================

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
