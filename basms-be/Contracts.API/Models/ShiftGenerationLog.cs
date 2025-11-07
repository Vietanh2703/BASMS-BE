namespace Contracts.API.Models;

/// <summary>
/// LOG TỰ ĐỘNG TẠO CA
/// Ghi lại lịch sử tự động tạo shifts từ contract schedules
/// Dùng để debug, audit và monitoring
/// </summary>
[Table("shift_generation_log")]
public class ShiftGenerationLog
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Thuộc hợp đồng nào
    /// </summary>
    public Guid ContractId { get; set; }

    /// <summary>
    /// Từ mẫu ca nào (optional)
    /// </summary>
    public Guid? ContractShiftScheduleId { get; set; }

    /// <summary>
    /// Ngày mục tiêu tạo shifts: 2025-01-15
    /// </summary>
    public DateTime GenerationDate { get; set; }

    /// <summary>
    /// Thời điểm chạy job tạo shifts
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Số lượng shifts tạo thành công
    /// </summary>
    public int ShiftsCreatedCount { get; set; } = 0;

    /// <summary>
    /// Số lượng shifts bị bỏ qua
    /// </summary>
    public int ShiftsSkippedCount { get; set; } = 0;

    /// <summary>
    /// Lý do bỏ qua (JSON array)
    /// Ví dụ: ["holiday", "location_closed", "exception_defined"]
    /// </summary>
    public string? SkipReasons { get; set; }

    /// <summary>
    /// Trạng thái: success, partial, failed
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Error message nếu failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Được tạo bởi: background_job, manual, api
    /// </summary>
    public string? GeneratedByJob { get; set; }
}
