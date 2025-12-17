namespace Shifts.API.ShiftsHandler.BulkCancelShift;

/// <summary>
/// Request để hủy nhiều ca trực cùng lúc
/// USE CASE: Nghỉ ốm dài ngày, thai sản, nghỉ phép dài hạn
/// </summary>
public record BulkCancelShiftRequest
{
    /// <summary>
    /// Guard nghỉ việc
    /// </summary>
    public Guid GuardId { get; init; }

    /// <summary>
    /// Ngày bắt đầu nghỉ
    /// </summary>
    public DateTime FromDate { get; init; }

    /// <summary>
    /// Ngày kết thúc nghỉ
    /// </summary>
    public DateTime ToDate { get; init; }

    /// <summary>
    /// Lý do nghỉ chi tiết (ví dụ: "Nghỉ thai sản 3 tháng", "Ốm dài ngày do COVID-19")
    /// </summary>
    public string CancellationReason { get; init; } = string.Empty;

    /// <summary>
    /// Loại nghỉ: SICK_LEAVE | MATERNITY_LEAVE | LONG_TERM_LEAVE | OTHER
    /// </summary>
    public string LeaveType { get; init; } = "OTHER";

    /// <summary>
    /// S3 URL ảnh chứng từ (đơn xin nghỉ, giấy khám bệnh, giấy thai sản, v.v.)
    /// Upload trước qua /api/files/upload
    /// </summary>
    public string? EvidenceImageUrl { get; init; }

    /// <summary>
    /// Manager thực hiện hủy
    /// </summary>
    public Guid CancelledBy { get; init; }
}
