namespace Contracts.API.Models;

/// <summary>
/// NGÀY ĐẶC BIỆT CỦA ĐỊA ĐIỂM
/// Các ngày location đóng cửa hoặc hoạt động khác thường
/// Ví dụ: Đóng cửa sửa chữa, Sự kiện đặc biệt, Nghỉ lễ riêng...
/// </summary>
[Table("location_special_days")]
public class LocationSpecialDay
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Thuộc địa điểm nào
    /// </summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// Ngày đặc biệt
    /// </summary>
    public DateTime SpecialDayDate { get; set; }

    /// <summary>
    /// Loại ngày: closed, half_day, extended_hours, event_day, maintenance
    /// </summary>
    public string DayType { get; set; } = string.Empty;

    /// <summary>
    /// Lý do: "Annual leave", "Renovation", "Special event"
    /// </summary>
    public string? Reason { get; set; }

    // ============================================================================
    // OVERRIDE GIỜ HOẠT ĐỘNG
    // ============================================================================

    /// <summary>
    /// Có hoạt động ngày này không?
    /// </summary>
    public bool IsOperating { get; set; } = false;

    /// <summary>
    /// Giờ mở cửa tùy chỉnh (nếu hoạt động)
    /// </summary>
    public TimeSpan? CustomStartTime { get; set; }

    /// <summary>
    /// Giờ đóng cửa tùy chỉnh
    /// </summary>
    public TimeSpan? CustomEndTime { get; set; }

    // ============================================================================
    /// ẢNH HƯỞNG ĐẾN SHIFTS
    // ============================================================================

    /// <summary>
    /// Có ảnh hưởng đến các ca làm việc thường xuyên không?
    /// true = hủy/điều chỉnh các ca thường xuyên
    /// </summary>
    public bool AffectsRegularShifts { get; set; } = true;

    /// <summary>
    /// Có cần tạo ca đặc biệt không?
    /// Ví dụ: Sự kiện cần nhiều bảo vệ hơn
    /// </summary>
    public bool RequiresSpecialShift { get; set; } = false;

    public string? Notes { get; set; }

    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
