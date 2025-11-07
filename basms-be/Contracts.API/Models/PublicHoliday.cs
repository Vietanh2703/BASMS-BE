namespace Contracts.API.Models;

/// <summary>
/// NGÀY LỄ QUỐC GIA
/// Danh sách ngày lễ Việt Nam (Tết, 30/4, 1/5, 2/9...)
/// Dùng để planning shifts và attendance
/// </summary>
[Table("public_holidays")]
public class PublicHoliday
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Ngày lễ: 2025-01-01
    /// </summary>
    public DateTime HolidayDate { get; set; }

    /// <summary>
    /// Tên ngày lễ tiếng Việt: "Tết Nguyên Đán", "Quốc khánh"
    /// </summary>
    public string HolidayName { get; set; } = string.Empty;

    /// <summary>
    /// Tên tiếng Anh: "Lunar New Year", "Independence Day"
    /// </summary>
    public string? HolidayNameEn { get; set; }

    /// <summary>
    /// Loại ngày lễ: national, tet, regional, substitute
    /// </summary>
    public string HolidayCategory { get; set; } = string.Empty;

    // ============================================================================
    // TẾT ĐẶC BIỆT (5 ngày liên tiếp)
    // ============================================================================

    /// <summary>
    /// Có phải ngày Tết không?
    /// </summary>
    public bool IsTetPeriod { get; set; } = false;

    /// <summary>
    /// Ngày thứ mấy của Tết: 1=Mùng 1, 2=Mùng 2... (thường 1-5)
    /// </summary>
    public int? TetDayNumber { get; set; }

    // ============================================================================
    // QUY ĐỊNH NGHỈ
    // ============================================================================

    /// <summary>
    /// Có phải ngày nghỉ chính thức theo luật không?
    /// </summary>
    public bool IsOfficialHoliday { get; set; } = true;

    /// <summary>
    /// Có được thực tế nghỉ không? (có thể bị dời)
    /// </summary>
    public bool IsObserved { get; set; } = true;

    /// <summary>
    /// Ngày gốc (nếu bị dời do trùng cuối tuần)
    /// Ví dụ: 30/4/2025 là T7 -> dời sang T2
    /// </summary>
    public DateTime? OriginalDate { get; set; }

    /// <summary>
    /// Ngày thực tế nghỉ (sau khi dời)
    /// </summary>
    public DateTime? ObservedDate { get; set; }

    // ============================================================================
    // PHẠM VI ÁP DỤNG
    // ============================================================================

    /// <summary>
    /// Áp dụng toàn quốc?
    /// </summary>
    public bool AppliesNationwide { get; set; } = true;

    /// <summary>
    /// Áp dụng cho khu vực nào? (JSON array: ["TP.HCM", "Hà Nội"])
    /// </summary>
    public string? AppliesToRegions { get; set; }

    // ============================================================================
    // ẢNH HƯỞNG CÔNG VIỆC
    // ============================================================================

    /// <summary>
    /// Các công sở thường đóng cửa không?
    /// </summary>
    public bool StandardWorkplacesClosed { get; set; } = true;

    /// <summary>
    /// Dịch vụ thiết yếu (bệnh viện, bảo vệ) vẫn hoạt động?
    /// </summary>
    public bool EssentialServicesOperating { get; set; } = true;

    /// <summary>
    /// Mô tả ngày lễ
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Năm: 2025
    /// </summary>
    public int Year { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
