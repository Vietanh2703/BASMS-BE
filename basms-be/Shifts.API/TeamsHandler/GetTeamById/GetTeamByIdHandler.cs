namespace Shifts.API.TeamsHandler.GetTeamById;


public record GetTeamByIdQuery(Guid TeamId) : IQuery<GetTeamByIdResult>;

public record GetTeamByIdResult
{
    public Guid TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public Guid ManagerId { get; init; }
    public string? ManagerName { get; init; }
    public string? Specialization { get; init; }
    public string? Description { get; init; }
    public int MinMembers { get; init; }
    public int? MaxMembers { get; init; }
    public int CurrentMemberCount { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<TeamMemberDto> Members { get; init; } = new();
}

public record TeamMemberDto
{
    public Guid TeamMemberId { get; init; }
    public Guid GuardId { get; init; }
    public string GuardName { get; init; } = string.Empty;
    public string EmployeeCode { get; init; } = string.Empty;
    public string? CertificationLevel { get; init; }
    public string Role { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int TotalShiftsAssigned { get; init; }
    public int TotalShiftsCompleted { get; init; }
    public decimal? AttendanceRate { get; init; }
    public DateTime JoinedAt { get; init; }
}

internal class GetTeamByIdHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetTeamByIdHandler> logger)
    : IQueryHandler<GetTeamByIdQuery, GetTeamByIdResult>
{
    public async Task<GetTeamByIdResult> Handle(
        GetTeamByIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting team {TeamId} with members", request.TeamId);

            using var connection = await dbFactory.CreateConnectionAsync();
            
            var teamQuery = @"
                SELECT
                    t.Id AS TeamId,
                    t.TeamCode,
                    t.TeamName,
                    t.ManagerId,
                    m.FullName AS ManagerName,
                    t.Specialization,
                    t.Description,
                    t.MinMembers,
                    t.MaxMembers,
                    t.CurrentMemberCount,
                    t.IsActive,
                    t.CreatedAt
                FROM teams t
                LEFT JOIN managers m ON t.ManagerId = m.Id
                WHERE t.Id = @TeamId
                  AND t.IsDeleted = 0";

            var team = await connection.QueryFirstOrDefaultAsync<GetTeamByIdResult>(
                teamQuery,
                new { request.TeamId });

            if (team == null)
            {
                logger.LogWarning("Team {TeamId} not found", request.TeamId);
                throw new InvalidOperationException($"Team {request.TeamId} không tồn tại");
            }
            
            var membersQuery = @"
                SELECT
                    tm.Id AS TeamMemberId,
                    tm.GuardId,
                    g.FullName AS GuardName,
                    g.EmployeeCode,
                    g.CertificationLevel,
                    tm.Role,
                    tm.IsActive,
                    tm.TotalShiftsAssigned,
                    tm.TotalShiftsCompleted,
                    tm.AttendanceRate,
                    tm.CreatedAt AS JoinedAt
                FROM team_members tm
                INNER JOIN guards g ON tm.GuardId = g.Id
                WHERE tm.TeamId = @TeamId
                  AND tm.IsDeleted = 0
                ORDER BY
                    CASE tm.Role
                        WHEN 'LEADER' THEN 1
                        WHEN 'DEPUTY' THEN 2
                        WHEN 'MEMBER' THEN 3
                        ELSE 4
                    END,
                    tm.CreatedAt ASC";

            var members = await connection.QueryAsync<TeamMemberDto>(
                membersQuery,
                new { request.TeamId });

            logger.LogInformation(
                "Found team {TeamCode} with {MemberCount} members",
                team.TeamCode,
                members.Count());

            return team with { Members = members.ToList() };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting team {TeamId}", request.TeamId);
            throw;
        }
    }
}
