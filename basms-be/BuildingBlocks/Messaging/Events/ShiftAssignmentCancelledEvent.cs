namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// Event được publish khi ShiftAssignment bị cancel
/// Attendances.API consume event này để update AttendanceRecords tương ứng
///
/// USE CASES:
/// - Hủy ca trực đơn lẻ
/// - Hủy nhiều ca cùng lúc (ốm dài ngày, thai sản)
/// - Background job protection (prevent tạo attendance cho assignment đã cancel)
/// </summary>
public record ShiftAssignmentCancelledEvent
{
    /// <summary>
    /// ID của shift assignment bị cancel
    /// </summary>
    public Guid ShiftAssignmentId { get; init; }

    /// <summary>
    /// Shift ID
    /// </summary>
    public Guid ShiftId { get; init; }

    /// <summary>
    /// Guard bị ảnh hưởng
    /// </summary>
    public Guid GuardId { get; init; }

    /// <summary>
    /// Lý do hủy (ốm dài ngày, thai sản, nghỉ phép dài hạn, v.v.)
    /// </summary>
    public string CancellationReason { get; init; } = string.Empty;

    /// <summary>
    /// Loại nghỉ: SICK_LEAVE | MATERNITY_LEAVE | LONG_TERM_LEAVE | OTHER
    /// </summary>
    public string LeaveType { get; init; } = "OTHER";

    /// <summary>
    /// Thời gian hủy
    /// </summary>
    public DateTime CancelledAt { get; init; }

    /// <summary>
    /// Manager hủy
    /// </summary>
    public Guid CancelledBy { get; init; }

    /// <summary>
    /// S3 URL ảnh chứng từ (đơn xin nghỉ, giấy khám bệnh, v.v.)
    /// </summary>
    public string? EvidenceImageUrl { get; init; }
}
