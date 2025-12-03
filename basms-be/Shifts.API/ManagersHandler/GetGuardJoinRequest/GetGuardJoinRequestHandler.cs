using Dapper;

namespace Shifts.API.ManagersHandler.GetGuardJoinRequest;

/// <summary>
/// Query để manager lấy danh sách guards đã gửi join request
/// </summary>
public record GetGuardJoinRequestQuery(
    Guid ManagerId
) : IQuery<GetGuardJoinRequestResult>;

/// <summary>
/// Result chứa danh sách guards pending approval
/// </summary>
public record GetGuardJoinRequestResult(
    Guid ManagerId,
    string ManagerName,
    int TotalRequests,
    List<GuardJoinRequestDto> PendingRequests
);

/// <summary>
/// DTO cho guard join request
/// </summary>
public record GuardJoinRequestDto(
    Guid GuardId,
    string EmployeeCode,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? AvatarUrl,
    DateTime? DateOfBirth,
    string? Gender,
    string EmploymentStatus,
    DateTime? HireDate,
    string? ContractType,
    string? PreferredShiftType,
    bool CanWorkOvertime,
    bool CanWorkWeekends,
    bool CanWorkHolidays,
    string CurrentAvailability,
    int TotalShiftsWorked,
    decimal TotalHoursWorked,
    decimal? AttendanceRate,
    decimal? PunctualityRate,
    int NoShowCount,
    int ViolationCount,
    int CommendationCount,
    DateTime? UpdatedAt,
    DateTime CreatedAt
);

internal class GetGuardJoinRequestHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetGuardJoinRequestHandler> logger)
    : IQueryHandler<GetGuardJoinRequestQuery, GetGuardJoinRequestResult>
{
    public async Task<GetGuardJoinRequestResult> Handle(
        GetGuardJoinRequestQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting join requests for Manager {ManagerId}",
                request.ManagerId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: VALIDATE MANAGER EXISTS
            // ================================================================
            var manager = await connection.QueryFirstOrDefaultAsync<Managers>(
                @"SELECT * FROM managers
                  WHERE Id = @ManagerId
                  AND IsDeleted = 0",
                new { request.ManagerId });

            if (manager == null)
            {
                logger.LogWarning("Manager {ManagerId} not found", request.ManagerId);
                throw new InvalidOperationException($"Manager with ID {request.ManagerId} not found");
            }

            // ================================================================
            // BƯỚC 2: GET ALL PENDING JOIN REQUESTS
            // ================================================================
            var pendingRequestsSql = @"
                SELECT
                    Id AS GuardId,
                    EmployeeCode,
                    FullName,
                    Email,
                    PhoneNumber,
                    AvatarUrl,
                    DateOfBirth,
                    Gender,
                    EmploymentStatus,
                    HireDate,
                    ContractType,
                    PreferredShiftType,
                    CanWorkOvertime,
                    CanWorkWeekends,
                    CanWorkHolidays,
                    CurrentAvailability,
                    TotalShiftsWorked,
                    TotalHoursWorked,
                    AttendanceRate,
                    PunctualityRate,
                    NoShowCount,
                    ViolationCount,
                    CommendationCount,
                    UpdatedAt,
                    CreatedAt
                FROM guards
                WHERE DirectManagerId = @ManagerId
                  AND ContractType = 'join_in_request'
                  AND IsDeleted = 0
                ORDER BY UpdatedAt DESC";

            var pendingGuards = await connection.QueryAsync<GuardJoinRequestDto>(
                pendingRequestsSql,
                new { request.ManagerId });

            var pendingGuardsList = pendingGuards.ToList();

            logger.LogInformation(
                "Found {Count} pending join requests for Manager {ManagerId} ({ManagerName})",
                pendingGuardsList.Count,
                request.ManagerId,
                manager.FullName);

            return new GetGuardJoinRequestResult(
                ManagerId: request.ManagerId,
                ManagerName: manager.FullName,
                TotalRequests: pendingGuardsList.Count,
                PendingRequests: pendingGuardsList
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting join requests for Manager {ManagerId}",
                request.ManagerId);
            throw;
        }
    }
}
