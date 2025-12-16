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
/// Result chứa status của attendance record
/// </summary>
public record GetAttendanceStatusByGuardIdResult
{
    public bool Success { get; init; }
    public Guid? AttendanceId { get; init; }
    public Guid GuardId { get; init; }
    public Guid ShiftId { get; init; }
    public string? Status { get; init; }
    public string? VerificationStatus { get; init; }
    public string? ErrorMessage { get; init; }
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
            // SQL QUERY - LẤY STATUS CỦA ATTENDANCE RECORD
            // ================================================================
            // Logic:
            // 1. Tìm attendance_record theo GuardId và ShiftId
            // 2. Chỉ trả về Status và VerificationStatus
            // ================================================================

            var sql = @"
                SELECT
                    Id AS AttendanceId,
                    GuardId,
                    ShiftId,
                    Status,
                    VerificationStatus
                FROM attendance_records
                WHERE
                    GuardId = @GuardId
                    AND ShiftId = @ShiftId
                    AND IsDeleted = 0
                LIMIT 1";

            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
                sql,
                new
                {
                    GuardId = request.GuardId,
                    ShiftId = request.ShiftId
                });

            if (result == null)
            {
                logger.LogInformation(
                    "No attendance record found for Guard {GuardId} and Shift {ShiftId}",
                    request.GuardId,
                    request.ShiftId);

                return new GetAttendanceStatusByGuardIdResult
                {
                    Success = true,
                    GuardId = request.GuardId,
                    ShiftId = request.ShiftId,
                    Status = null,
                    VerificationStatus = null,
                    ErrorMessage = "No attendance record found"
                };
            }

            logger.LogInformation(
                "Found attendance status: {Status} for Guard {GuardId} and Shift {ShiftId}",
                (string)result.Status,
                request.GuardId,
                request.ShiftId);

            return new GetAttendanceStatusByGuardIdResult
            {
                Success = true,
                AttendanceId = result.AttendanceId,
                GuardId = result.GuardId,
                ShiftId = result.ShiftId,
                Status = result.Status,
                VerificationStatus = result.VerificationStatus
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting attendance status for Guard {GuardId} and Shift {ShiftId}",
                request.GuardId,
                request.ShiftId);

            return new GetAttendanceStatusByGuardIdResult
            {
                Success = false,
                GuardId = request.GuardId,
                ShiftId = request.ShiftId,
                ErrorMessage = $"Failed to get attendance status: {ex.Message}"
            };
        }
    }
}
