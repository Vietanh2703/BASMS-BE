namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// Event được publish khi Guard check-out thành công
/// Shifts.API sẽ consume event này để update ShiftAssignment và đánh dấu ca làm đã hoàn thành
/// </summary>
public record GuardCheckedOutEvent
{
    /// <summary>
    /// ID của shift assignment
    /// </summary>
    public Guid ShiftAssignmentId { get; init; }

    /// <summary>
    /// ID của shift
    /// </summary>
    public Guid ShiftId { get; init; }

    /// <summary>
    /// ID của guard
    /// </summary>
    public Guid GuardId { get; init; }

    /// <summary>
    /// Thời gian check-out (Vietnam timezone)
    /// </summary>
    public DateTime CheckOutTime { get; init; }

    /// <summary>
    /// Thời gian completed (same as CheckOutTime)
    /// </summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>
    /// Guard có về sớm không
    /// </summary>
    public bool IsEarlyLeave { get; init; }

    /// <summary>
    /// Số phút về sớm
    /// </summary>
    public int EarlyLeaveMinutes { get; init; }

    /// <summary>
    /// Guard có làm ngoài giờ không
    /// </summary>
    public bool HasOvertime { get; init; }

    /// <summary>
    /// Số phút làm ngoài giờ
    /// </summary>
    public int OvertimeMinutes { get; init; }

    /// <summary>
    /// Tổng thời gian làm việc thực tế (phút)
    /// </summary>
    public int ActualWorkDurationMinutes { get; init; }

    /// <summary>
    /// Tổng số giờ làm (đã trừ break, làm tròn 2 chữ số)
    /// </summary>
    public decimal TotalHours { get; init; }

    /// <summary>
    /// Face match score
    /// </summary>
    public float FaceMatchScore { get; init; }

    /// <summary>
    /// Khoảng cách từ công trường (meters)
    /// </summary>
    public double DistanceFromSite { get; init; }
}
