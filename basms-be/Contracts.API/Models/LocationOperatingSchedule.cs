namespace Contracts.API.Models;

/// <summary>
/// LỊCH HOẠT ĐỘNG CỦA ĐỊA ĐIỂM
/// Định nghĩa giờ mở cửa/đóng cửa theo từng ngày trong tuần
/// Ví dụ: T2-T6: 8h-17h, T7: 8h-12h, CN: đóng cửa
/// </summary>
[Table("location_operating_schedule")]
public class LocationOperatingSchedule
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Thuộc địa điểm nào
    /// </summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// Tên lịch: "Regular Hours", "Summer Hours", "Holiday Hours"
    /// </summary>
    public string ScheduleName { get; set; } = string.Empty;

    /// <summary>
    /// Ngày trong tuần: 1=T2, 2=T3, 3=T4, 4=T5, 5=T6, 6=T7, 7=CN
    /// </summary>
    public int DayOfWeek { get; set; }

    /// <summary>
    /// Địa điểm có hoạt động ngày này không?
    /// false = đóng cửa cả ngày
    /// </summary>
    public bool IsOperating { get; set; } = true;

    // ============================================================================
    // KHUNG GIỜ HOẠT ĐỘNG
    // Hỗ trợ tới 3 khung giờ/ngày (cho các địa điểm làm ca 3)
    // ============================================================================

    /// <summary>
    /// Khung giờ 1 - Bắt đầu: 08:00:00
    /// </summary>
    public TimeSpan? TimeSlot1Start { get; set; }

    /// <summary>
    /// Khung giờ 1 - Kết thúc: 17:00:00
    /// </summary>
    public TimeSpan? TimeSlot1End { get; set; }

    /// <summary>
    /// Khung giờ 2 - Bắt đầu (optional, cho split schedule)
    /// Ví dụ: nghỉ trưa 11:30-13:00, slot 2 bắt đầu 13:00
    /// </summary>
    public TimeSpan? TimeSlot2Start { get; set; }

    /// <summary>
    /// Khung giờ 2 - Kết thúc
    /// </summary>
    public TimeSpan? TimeSlot2End { get; set; }

    /// <summary>
    /// Khung giờ 3 - Bắt đầu (optional, cho 3-shift locations)
    /// </summary>
    public TimeSpan? TimeSlot3Start { get; set; }

    /// <summary>
    /// Khung giờ 3 - Kết thúc
    /// </summary>
    public TimeSpan? TimeSlot3End { get; set; }

    // ============================================================================
    // THỜI GIAN HIỆU LỰC
    // ============================================================================

    /// <summary>
    /// Có hiệu lực từ ngày
    /// </summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// Có hiệu lực đến ngày (NULL = vĩnh viễn)
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>
    /// Ghi chú
    /// </summary>
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
