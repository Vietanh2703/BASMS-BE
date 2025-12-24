using Dapper.Contrib.Extensions;

namespace Attendances.API.Models;


[Table("attendance_records")]
public class AttendanceRecords
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid ShiftAssignmentId { get; set; }
    public Guid GuardId { get; set; }
    public Guid ShiftId { get; set; }
    public DateTime? CheckInTime { get; set; }
    public decimal? CheckInLatitude { get; set; }
    public decimal? CheckInLongitude { get; set; }
    public decimal? CheckInLocationAccuracy { get; set; }
    public decimal? CheckInDistanceFromSite { get; set; }
    public string? CheckInDeviceId { get; set; }
    public string? CheckInFaceImageUrl { get; set; }
    public decimal? CheckInFaceMatchScore { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public decimal? CheckOutLatitude { get; set; }
    public decimal? CheckOutLongitude { get; set; }
    public decimal? CheckOutLocationAccuracy { get; set; }
    public decimal? CheckOutDistanceFromSite { get; set; }
    public string? CheckOutDeviceId { get; set; }
    public string? CheckOutFaceImageUrl { get; set; }
    public decimal? CheckOutFaceMatchScore { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ScheduledEndTime { get; set; }
    public int? ActualWorkDurationMinutes { get; set; }
    public int BreakDurationMinutes { get; set; } = 60;
    public decimal? TotalHours { get; set; }
    public string Status { get; set; } = "CHECKED_IN";
    public bool IsLate { get; set; } = false;
    public bool IsEarlyLeave { get; set; } = false;
    public bool HasOvertime { get; set; } = false;
    public bool IsIncomplete { get; set; } = false;
    public bool IsVerified { get; set; } = false;
    public int? LateMinutes { get; set; }
    public int? EarlyLeaveMinutes { get; set; }
    public int? OvertimeMinutes { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string VerificationStatus { get; set; } = "PENDING";
    public string? Notes { get; set; }
    public string? ManagerNotes { get; set; }
    public bool AutoDetected { get; set; } = false;
    public bool FlagsForReview { get; set; } = false;
    public string? FlagReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

}
