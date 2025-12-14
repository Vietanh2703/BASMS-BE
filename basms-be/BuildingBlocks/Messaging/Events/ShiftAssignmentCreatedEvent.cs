namespace BuildingBlocks.Messaging.Events;

/// <summary>
/// Event được publish khi ShiftAssignment được tạo
/// Attendances.API sẽ consume event này để tạo AttendanceRecord tương ứng
/// </summary>
public record ShiftAssignmentCreatedEvent
{
    /// <summary>
    /// ID của shift assignment vừa tạo
    /// </summary>
    public Guid ShiftAssignmentId { get; init; }

    /// <summary>
    /// ID của shift
    /// </summary>
    public Guid ShiftId { get; init; }

    /// <summary>
    /// ID của guard được assign
    /// </summary>
    public Guid GuardId { get; init; }

    /// <summary>
    /// ID của team (nếu là team assignment)
    /// </summary>
    public Guid? TeamId { get; init; }

    /// <summary>
    /// Thời gian bắt đầu dự kiến (từ shift)
    /// </summary>
    public DateTime ScheduledStartTime { get; init; }

    /// <summary>
    /// Thời gian kết thúc dự kiến (từ shift)
    /// </summary>
    public DateTime ScheduledEndTime { get; init; }

    /// <summary>
    /// Location ID (để lưu vào attendance record)
    /// </summary>
    public Guid LocationId { get; init; }

    /// <summary>
    /// Location latitude (cho geofencing check-in/out)
    /// </summary>
    public decimal? LocationLatitude { get; init; }

    /// <summary>
    /// Location longitude (cho geofencing check-in/out)
    /// </summary>
    public decimal? LocationLongitude { get; init; }
}
