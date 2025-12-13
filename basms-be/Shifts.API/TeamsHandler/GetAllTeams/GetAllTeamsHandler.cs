namespace Shifts.API.TeamsHandler.GetAllTeams;

/// <summary>
/// Query để lấy danh sách teams theo manager
/// </summary>
public record GetAllTeamsQuery(
    Guid ManagerId                  // Manager ID (required)
) : IQuery<GetAllTeamsResult>;

/// <summary>
/// Result chứa danh sách teams
/// </summary>
public record GetAllTeamsResult
{
    public List<TeamSummaryDto> Teams { get; init; } = new();
    public int TotalCount { get; init; }
}

public record TeamSummaryDto
{
    public Guid TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public Guid ManagerId { get; init; }
    public string? ManagerName { get; init; }
    public string? Specialization { get; init; }
    public int CurrentMemberCount { get; init; }
    public int MinMembers { get; init; }
    public int? MaxMembers { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}

internal class GetAllTeamsHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetAllTeamsHandler> logger)
    : IQueryHandler<GetAllTeamsQuery, GetAllTeamsResult>
{
    public async Task<GetAllTeamsResult> Handle(
        GetAllTeamsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting all teams for manager {ManagerId}",
                request.ManagerId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // QUERY TEAMS BY MANAGER
            // ================================================================
            var teamsQuery = @"
                SELECT
                    t.Id AS TeamId,
                    t.TeamCode,
                    t.TeamName,
                    t.ManagerId,
                    m.FullName AS ManagerName,
                    t.Specialization,
                    t.CurrentMemberCount,
                    t.MinMembers,
                    t.MaxMembers,
                    t.IsActive,
                    t.CreatedAt
                FROM teams t
                LEFT JOIN managers m ON t.ManagerId = m.Id
                WHERE t.ManagerId = @ManagerId
                  AND t.IsDeleted = 0
                ORDER BY t.CreatedAt DESC";

            var teams = await connection.QueryAsync<TeamSummaryDto>(
                teamsQuery,
                new { ManagerId = request.ManagerId });

            logger.LogInformation(
                "✓ Returning {Count} teams for manager {ManagerId}",
                teams.Count(),
                request.ManagerId);

            return new GetAllTeamsResult
            {
                Teams = teams.ToList(),
                TotalCount = teams.Count()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting teams");
            throw;
        }
    }
}
