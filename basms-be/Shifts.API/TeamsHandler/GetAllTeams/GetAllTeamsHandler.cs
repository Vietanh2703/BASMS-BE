using Dapper;
using Shifts.API.Data;

namespace Shifts.API.TeamsHandler.GetAllTeams;

/// <summary>
/// Query để lấy danh sách teams với filter
/// </summary>
public record GetAllTeamsQuery(
    Guid? ManagerId,                // Filter theo manager (optional)
    string? Specialization,         // Filter theo specialization (optional)
    bool? IsActive                  // Filter theo active status (optional)
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
                "Getting all teams (ManagerId: {ManagerId}, Specialization: {Specialization}, IsActive: {IsActive})",
                request.ManagerId,
                request.Specialization,
                request.IsActive);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BUILD QUERY WITH FILTERS
            // ================================================================
            var whereConditions = new List<string> { "t.IsDeleted = 0" };
            var parameters = new DynamicParameters();

            if (request.ManagerId.HasValue)
            {
                whereConditions.Add("t.ManagerId = @ManagerId");
                parameters.Add("ManagerId", request.ManagerId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Specialization))
            {
                whereConditions.Add("t.Specialization = @Specialization");
                parameters.Add("Specialization", request.Specialization.ToUpper());
            }

            if (request.IsActive.HasValue)
            {
                whereConditions.Add("t.IsActive = @IsActive");
                parameters.Add("IsActive", request.IsActive.Value);
            }

            var whereClause = string.Join(" AND ", whereConditions);

            // ================================================================
            // QUERY ALL TEAMS
            // ================================================================
            var teamsQuery = $@"
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
                WHERE {whereClause}
                ORDER BY t.CreatedAt DESC";

            var teams = await connection.QueryAsync<TeamSummaryDto>(teamsQuery, parameters);

            logger.LogInformation(
                "✓ Returning {Count} teams",
                teams.Count());

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
