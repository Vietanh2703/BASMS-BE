using Dapper;

namespace Attendances.API.AttendanceHandler.GetAllAttendanceRecords;

/// <summary>
/// Query để lấy danh sách tất cả attendance records
/// Sắp xếp theo: CheckInTime giảm dần (mới nhất trước)
/// </summary>
public record GetAllAttendanceRecordsQuery() : IQuery<GetAllAttendanceRecordsResult>;

/// <summary>
/// Result chứa danh sách attendance records
/// </summary>
public record GetAllAttendanceRecordsResult
{
    public bool Success { get; init; }
    public List<AttendanceRecordDto> Records { get; init; } = new();
    public int TotalCount { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO cho attendance record
/// </summary>
public record AttendanceRecordDto
{
    public Guid Id { get; init; }
    public Guid ShiftAssignmentId { get; init; }
    public Guid GuardId { get; init; }
    public Guid ShiftId { get; init; }

    // Check-in Info
    public DateTime CheckInTime { get; init; }
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
/// Handler để lấy danh sách tất cả attendance records
/// </summary>
internal class GetAllAttendanceRecordsHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetAllAttendanceRecordsHandler> logger)
    : IQueryHandler<GetAllAttendanceRecordsQuery, GetAllAttendanceRecordsResult>
{
    public async Task<GetAllAttendanceRecordsResult> Handle(
        GetAllAttendanceRecordsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting all attendance records");

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // COUNT TOTAL RECORDS
            // ================================================================
            var countSql = @"
                SELECT COUNT(*)
                FROM attendance_records
                WHERE IsDeleted = 0";

            var totalCount = await connection.ExecuteScalarAsync<int>(countSql);

            logger.LogInformation(
                "Total attendance records found: {TotalCount}",
                totalCount);

            // ================================================================
            // GET ALL DATA - SORTED BY CHECK-IN TIME DESCENDING
            // ================================================================

            var sql = @"
                SELECT
                    Id,
                    ShiftAssignmentId,
                    GuardId,
                    ShiftId,
                    CheckInTime,
                    CheckInLatitude,
                    CheckInLongitude,
                    CheckInLocationAccuracy,
                    CheckInDistanceFromSite,
                    CheckInDeviceId,
                    CheckInFaceImageUrl,
                    CheckInFaceMatchScore,
                    CheckOutTime,
                    CheckOutLatitude,
                    CheckOutLongitude,
                    CheckOutLocationAccuracy,
                    CheckOutDistanceFromSite,
                    CheckOutDeviceId,
                    CheckOutFaceImageUrl,
                    CheckOutFaceMatchScore,
                    ScheduledStartTime,
                    ScheduledEndTime,
                    ActualWorkDurationMinutes,
                    BreakDurationMinutes,
                    TotalHours,
                    Status,
                    IsLate,
                    IsEarlyLeave,
                    HasOvertime,
                    IsIncomplete,
                    IsVerified,
                    LateMinutes,
                    EarlyLeaveMinutes,
                    OvertimeMinutes,
                    VerifiedBy,
                    VerifiedAt,
                    VerificationStatus,
                    Notes,
                    ManagerNotes,
                    AutoDetected,
                    FlagsForReview,
                    FlagReason,
                    CreatedAt,
                    UpdatedAt
                FROM attendance_records
                WHERE IsDeleted = 0
                ORDER BY
                    CheckInTime DESC";

            var records = await connection.QueryAsync<AttendanceRecordDto>(sql);
            var recordsList = records.ToList();

            logger.LogInformation(
                "Retrieved {Count} attendance records sorted by check-in time (newest first)",
                recordsList.Count);

            return new GetAllAttendanceRecordsResult
            {
                Success = true,
                Records = recordsList,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all attendance records");

            return new GetAllAttendanceRecordsResult
            {
                Success = false,
                Records = new List<AttendanceRecordDto>(),
                TotalCount = 0,
                ErrorMessage = $"Failed to get attendance records: {ex.Message}"
            };
        }
    }
}
