using Dapper;

namespace Attendances.API.AttendanceHandler.GetAttendanceStatusByGuardId;

/// <summary>
/// Query để lấy status của attendance record theo GuardId và ShiftId
/// </summary>
public record GetAttendanceStatusByGuardIdQuery(
    Guid GuardId,
    Guid ShiftId
) : IQuery<GetAttendanceStatusByGuardIdResult>;

/// <summary>
/// Result chứa đầy đủ thông tin của attendance record
/// </summary>
public record GetAttendanceStatusByGuardIdResult
{
    public bool Success { get; init; }
    public AttendanceDetailDto? Attendance { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO chứa đầy đủ thông tin attendance record
/// </summary>
public record AttendanceDetailDto
{
    public Guid Id { get; init; }
    public Guid ShiftAssignmentId { get; init; }
    public Guid GuardId { get; init; }
    public Guid ShiftId { get; init; }

    // Check-in Info
    public DateTime? CheckInTime { get; init; }
    public decimal? CheckInLatitude { get; init; }
    public decimal? CheckInLongitude { get; init; }
    public decimal? CheckInLocationAccuracy { get; init; }
    public decimal? CheckInDistanceFromSite { get; init; }
    public string? CheckInDeviceId { get; init; }
    public string? CheckInFaceImageUrl { get; init; }
    public decimal? CheckInFaceMatchScore { get; init; }

    // Check-out Info
    public DateTime? CheckOutTime { get; init; }
    public decimal? CheckOutLatitude { get; init; }
    public decimal? CheckOutLongitude { get; init; }
    public decimal? CheckOutLocationAccuracy { get; init; }
    public decimal? CheckOutDistanceFromSite { get; init; }
    public string? CheckOutDeviceId { get; init; }
    public string? CheckOutFaceImageUrl { get; init; }
    public decimal? CheckOutFaceMatchScore { get; init; }

    // Scheduled Time
    public DateTime? ScheduledStartTime { get; init; }
    public DateTime? ScheduledEndTime { get; init; }

    // Duration
    public int? ActualWorkDurationMinutes { get; init; }
    public int BreakDurationMinutes { get; init; }
    public decimal? TotalHours { get; init; }

    // Status & Flags
    public string Status { get; init; } = string.Empty;
    public bool IsLate { get; init; }
    public bool IsEarlyLeave { get; init; }
    public bool HasOvertime { get; init; }
    public bool IsIncomplete { get; init; }
    public bool IsVerified { get; init; }

    // Late/Early Minutes
    public int? LateMinutes { get; init; }
    public int? EarlyLeaveMinutes { get; init; }
    public int? OvertimeMinutes { get; init; }

    // Verification
    public Guid? VerifiedBy { get; init; }
    public DateTime? VerifiedAt { get; init; }
    public string VerificationStatus { get; init; } = string.Empty;

    // Notes
    public string? Notes { get; init; }
    public string? ManagerNotes { get; init; }

    // Flags
    public bool AutoDetected { get; init; }
    public bool FlagsForReview { get; init; }
    public string? FlagReason { get; init; }

    // Audit
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Handler để lấy status của attendance record
/// </summary>
internal class GetAttendanceStatusByGuardIdHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetAttendanceStatusByGuardIdHandler> logger)
    : IQueryHandler<GetAttendanceStatusByGuardIdQuery, GetAttendanceStatusByGuardIdResult>
{
    public async Task<GetAttendanceStatusByGuardIdResult> Handle(
        GetAttendanceStatusByGuardIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting attendance status for Guard {GuardId} and Shift {ShiftId}",
                request.GuardId,
                request.ShiftId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // SQL QUERY - LẤY TẤT CẢ THÔNG TIN CỦA ATTENDANCE RECORD
            // ================================================================
            // Logic:
            // 1. Tìm attendance_record theo GuardId và ShiftId
            // 2. Trả về tất cả thông tin của attendance record
            // ================================================================

            var sql = @"
                SELECT
                    Id,
                    ShiftAssignmentId,
                    GuardId,
                    ShiftId,

                    -- Check-in Info
                    CheckInTime,
                    CheckInLatitude,
                    CheckInLongitude,
                    CheckInLocationAccuracy,
                    CheckInDistanceFromSite,
                    CheckInDeviceId,
                    CheckInFaceImageUrl,
                    CheckInFaceMatchScore,

                    -- Check-out Info
                    CheckOutTime,
                    CheckOutLatitude,
                    CheckOutLongitude,
                    CheckOutLocationAccuracy,
                    CheckOutDistanceFromSite,
                    CheckOutDeviceId,
                    CheckOutFaceImageUrl,
                    CheckOutFaceMatchScore,

                    -- Scheduled Time
                    ScheduledStartTime,
                    ScheduledEndTime,

                    -- Duration
                    ActualWorkDurationMinutes,
                    BreakDurationMinutes,
                    TotalHours,

                    -- Status & Flags
                    Status,
                    IsLate,
                    IsEarlyLeave,
                    HasOvertime,
                    IsIncomplete,
                    IsVerified,

                    -- Late/Early Minutes
                    LateMinutes,
                    EarlyLeaveMinutes,
                    OvertimeMinutes,

                    -- Verification
                    VerifiedBy,
                    VerifiedAt,
                    VerificationStatus,

                    -- Notes
                    Notes,
                    ManagerNotes,

                    -- Flags
                    AutoDetected,
                    FlagsForReview,
                    FlagReason,

                    -- Audit
                    CreatedAt,
                    UpdatedAt

                FROM attendance_records
                WHERE
                    GuardId = @GuardId
                    AND ShiftId = @ShiftId
                    AND IsDeleted = 0
                LIMIT 1";

            var attendance = await connection.QueryFirstOrDefaultAsync<AttendanceDetailDto>(
                sql,
                new
                {
                    GuardId = request.GuardId,
                    ShiftId = request.ShiftId
                });

            if (attendance == null)
            {
                logger.LogInformation(
                    "No attendance record found for Guard {GuardId} and Shift {ShiftId}",
                    request.GuardId,
                    request.ShiftId);

                return new GetAttendanceStatusByGuardIdResult
                {
                    Success = true,
                    Attendance = null,
                    ErrorMessage = "No attendance record found"
                };
            }

            logger.LogInformation(
                "Found attendance record with status: {Status} for Guard {GuardId} and Shift {ShiftId}",
                attendance.Status,
                request.GuardId,
                request.ShiftId);

            return new GetAttendanceStatusByGuardIdResult
            {
                Success = true,
                Attendance = attendance
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting attendance record for Guard {GuardId} and Shift {ShiftId}",
                request.GuardId,
                request.ShiftId);

            return new GetAttendanceStatusByGuardIdResult
            {
                Success = false,
                Attendance = null,
                ErrorMessage = $"Failed to get attendance record: {ex.Message}"
            };
        }
    }
}
