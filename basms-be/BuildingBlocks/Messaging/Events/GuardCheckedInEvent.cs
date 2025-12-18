namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// Event được publish khi Guard check-in thành công
/// Shifts.API sẽ consume event này để update ShiftAssignment
/// </summary>
public record GuardCheckedInEvent
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
    /// Thời gian check-in (Vietnam timezone)
    /// </summary>
    public DateTime CheckInTime { get; init; }

    /// <summary>
    /// Thời gian confirmed (same as CheckInTime)
    /// </summary>
    public DateTime ConfirmedAt { get; init; }

    /// <summary>
    /// Guard có late không
    /// </summary>
    public bool IsLate { get; init; }

    /// <summary>
    /// Số phút late
    /// </summary>
    public int LateMinutes { get; init; }

    /// <summary>
    /// Face match score
    /// </summary>
    public float FaceMatchScore { get; init; }

    /// <summary>
    /// Khoảng cách từ công trường (meters)
    /// </summary>
    public double DistanceFromSite { get; init; }
}
