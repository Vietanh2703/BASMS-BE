using Shifts.API.Utilities;

namespace Shifts.API.GuardsHandler.CheckGuardTeamStatus;

public record CheckGuardTeamStatusQuery(Guid GuardId) : IQuery<CheckGuardTeamStatusResult>;

public record CheckGuardTeamStatusResult
{
    public Guid GuardId { get; init; }
    public string GuardName { get; init; } = string.Empty;
    public string EmployeeCode { get; init; } = string.Empty;
    public bool IsInActiveTeam { get; init; }
    public TeamMembershipInfo? CurrentTeam { get; init; }
}

public record TeamMembershipInfo
{
    public Guid TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public DateTime JoinedAt { get; init; }
    public int TotalShiftsAssigned { get; init; }
    public int TotalShiftsCompleted { get; init; }
}

internal class CheckGuardTeamStatusHandler(
    IDbConnectionFactory dbFactory,
    ILogger<CheckGuardTeamStatusHandler> logger)
    : IQueryHandler<CheckGuardTeamStatusQuery, CheckGuardTeamStatusResult>
{
    public async Task<CheckGuardTeamStatusResult> Handle(
        CheckGuardTeamStatusQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Checking team status for guard {GuardId}",
                request.GuardId);

            using var connection = await dbFactory.CreateConnectionAsync();

            var guard = await connection.GetGuardByIdOrThrowAsync(request.GuardId);

            logger.LogInformation(
                "Guard found: {FullName} ({EmployeeCode})",
                guard.FullName,
                guard.EmployeeCode);

            var teamMembership = await connection.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT
                    tm.Id AS MembershipId,
                    tm.TeamId,
                    tm.Role,
                    tm.CreatedAt AS JoinedAt,
                    tm.TotalShiftsAssigned,
                    tm.TotalShiftsCompleted,
                    t.TeamCode,
                    t.TeamName
                  FROM team_members tm
                  INNER JOIN teams t ON t.Id = tm.TeamId
                  WHERE tm.GuardId = @GuardId
                    AND tm.IsActive = 1
                    AND tm.IsDeleted = 0
                    AND t.IsActive = 1
                    AND t.IsDeleted = 0
                  ORDER BY tm.CreatedAt DESC
                  LIMIT 1",
                new { GuardId = request.GuardId });

            TeamMembershipInfo? currentTeam = null;
            bool isInActiveTeam = false;

            if (teamMembership != null)
            {
                isInActiveTeam = true;
                currentTeam = new TeamMembershipInfo
                {
                    TeamId = (Guid)teamMembership.TeamId,
                    TeamCode = (string)teamMembership.TeamCode,
                    TeamName = (string)teamMembership.TeamName,
                    Role = (string)teamMembership.Role,
                    JoinedAt = (DateTime)teamMembership.JoinedAt,
                    TotalShiftsAssigned = (int)teamMembership.TotalShiftsAssigned,
                    TotalShiftsCompleted = (int)teamMembership.TotalShiftsCompleted
                };

                logger.LogInformation(
                    "Guard {GuardId} is in active team: {TeamCode} - {TeamName} (Role: {Role})",
                    request.GuardId,
                    currentTeam.TeamCode,
                    currentTeam.TeamName,
                    currentTeam.Role);
            }
            else
            {
                logger.LogInformation(
                    "Guard {GuardId} is not in any active team",
                    request.GuardId);
            }

            return new CheckGuardTeamStatusResult
            {
                GuardId = guard.Id,
                GuardName = guard.FullName,
                EmployeeCode = guard.EmployeeCode,
                IsInActiveTeam = isInActiveTeam,
                CurrentTeam = currentTeam
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking guard team status");
            throw;
        }
    }
}
