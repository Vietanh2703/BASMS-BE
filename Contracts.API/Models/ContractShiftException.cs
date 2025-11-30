namespace Contracts.API.Models;

/// <summary>
/// NGOẠI LỆ CA TRONG HỢP ĐỒNG
/// Các trường hợp đặc biệt không theo mẫu ca thường xuyên
/// Ví dụ: Ngày 30/4 bỏ ca sáng, ngày sự kiện tăng số bảo vệ lên 5...
/// </summary>
[Table("contract_shift_exceptions")]
public class ContractShiftException
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Thuộc mẫu ca nào
    /// </summary>
    public Guid ContractShiftScheduleId { get; set; }

    /// <summary>
    /// Ngày ngoại lệ: 2025-04-30
    /// </summary>
    public DateTime ExceptionDate { get; set; }

    /// <summary>
    /// Loại ngoại lệ: skip, modify, replace
    /// - skip: bỏ qua không tạo ca
    /// - modify: điều chỉnh giờ giấc hoặc số người
    /// - replace: thay thế hoàn toàn
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>
    /// Lý do: "Holiday", "Maintenance", "Customer request", "Special event"
    /// </summary>
    public string? Reason { get; set; }

    // ============================================================================
    // NẾU MODIFY/REPLACE - Các giá trị override
    // ============================================================================

    /// <summary>
    /// Giờ bắt đầu mới (nếu modify/replace)
    /// </summary>
    public TimeSpan? ModifiedStartTime { get; set; }

    /// <summary>
    /// Giờ kết thúc mới (nếu modify/replace)
    /// </summary>
    public TimeSpan? ModifiedEndTime { get; set; }

    /// <summary>
    /// Số lượng bảo vệ mới (nếu modify/replace)
    /// Ví dụ: bình thường 2, sự kiện cần 5
    /// </summary>
    public int? ModifiedGuardsCount { get; set; }

    /// <summary>
    /// Hướng dẫn đặc biệt cho ngày này
    /// </summary>
    public string? SpecialInstructions { get; set; }

    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
