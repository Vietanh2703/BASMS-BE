using Dapper;
using Shifts.API.Data;
using Shifts.API.TeamsHandler.GetTeamById;

namespace Shifts.API.TeamsHandler.GetTeamMembers;

/// <summary>
/// Query để lấy danh sách members của một team
/// </summary>
public record GetTeamMembersQuery(Guid TeamId) : IQuery<GetTeamMembersResult>;

/// <summary>
/// Result chứa danh sách team members
/// </summary>
public record GetTeamMembersResult
{
    public bool Success { get; init; }
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public string TeamCode { get; init; } = string.Empty;
    public int TotalMembers { get; init; }
    public List<TeamMemberDto> Members { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

internal class GetTeamMembersHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetTeamMembersHandler> logger)
    : IQueryHandler<GetTeamMembersQuery, GetTeamMembersResult>
{
    public async Task<GetTeamMembersResult> Handle(
        GetTeamMembersQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting members for team {TeamId}", request.TeamId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: VALIDATE TEAM EXISTS
            // ================================================================
            var teamInfo = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT Id, TeamCode, TeamName, IsDeleted
                FROM teams
                WHERE Id = @TeamId",
                new { request.TeamId });

            if (teamInfo == null)
            {
                logger.LogWarning("Team {TeamId} not found", request.TeamId);
                return new GetTeamMembersResult
                {
                    Success = false,
                    TeamId = request.TeamId,
                    ErrorMessage = $"Team {request.TeamId} không tồn tại"
                };
            }

            if (teamInfo.IsDeleted == 1)
            {
                logger.LogWarning("Team {TeamId} is deleted", request.TeamId);
                return new GetTeamMembersResult
                {
                    Success = false,
                    TeamId = request.TeamId,
                    TeamName = (string)teamInfo.TeamName,
                    TeamCode = (string)teamInfo.TeamCode,
                    ErrorMessage = "Team đã bị xóa"
                };
            }

            logger.LogInformation(
                "Team found: {TeamCode} - {TeamName}",
                (string)teamInfo.TeamCode,
                (string)teamInfo.TeamName);

            // ================================================================
            // BƯỚC 2: QUERY TEAM MEMBERS
            // ================================================================
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

            var membersList = members.ToList();

            logger.LogInformation(
                "Found {MemberCount} members in team {TeamCode}",
                membersList.Count,
                (string)teamInfo.TeamCode);

            return new GetTeamMembersResult
            {
                Success = true,
                TeamId = request.TeamId,
                TeamName = (string)teamInfo.TeamName,
                TeamCode = (string)teamInfo.TeamCode,
                TotalMembers = membersList.Count,
                Members = membersList
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting members for team {TeamId}", request.TeamId);

            return new GetTeamMembersResult
            {
                Success = false,
                TeamId = request.TeamId,
                ErrorMessage = $"Lỗi khi lấy danh sách members: {ex.Message}"
            };
        }
    }
}
