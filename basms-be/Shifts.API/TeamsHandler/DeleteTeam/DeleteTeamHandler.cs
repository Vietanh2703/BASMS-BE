using Dapper;
using Dapper.Contrib.Extensions;

namespace Shifts.API.TeamsHandler.DeleteTeam;

/// <summary>
/// Command để xóa team (soft delete)
/// </summary>
public record DeleteTeamCommand(
    Guid TeamId,
    Guid DeletedBy  // Manager xóa team
) : ICommand<DeleteTeamResult>;

/// <summary>
/// Result của delete team operation
/// </summary>
public record DeleteTeamResult(
    bool Success,
    string Message
);

internal class DeleteTeamHandler(
    IDbConnectionFactory dbFactory,
    ILogger<DeleteTeamHandler> logger)
    : ICommandHandler<DeleteTeamCommand, DeleteTeamResult>
{
    public async Task<DeleteTeamResult> Handle(
        DeleteTeamCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Soft deleting team {TeamId}", request.TeamId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: VALIDATE TEAM EXISTS
            // ================================================================
            var team = await connection.GetAsync<Models.Teams>(request.TeamId);

            if (team == null || team.IsDeleted)
            {
                logger.LogWarning("Team {TeamId} not found or already deleted", request.TeamId);
                return new DeleteTeamResult(
                    false,
                    "Team không tồn tại hoặc đã bị xóa");
            }

            logger.LogInformation(
                "Found team: {TeamCode} - {TeamName}",
                team.TeamCode,
                team.TeamName);

            // ================================================================
            // BƯỚC 2: VALIDATE - CHECK IF TEAM HAS BEEN ASSIGNED TO SHIFTS
            // ================================================================
            var assignmentCount = await connection.QueryFirstOrDefaultAsync<int>(@"
                SELECT COUNT(1)
                FROM shift_assignments
                WHERE TeamId = @TeamId
                  AND IsDeleted = 0",
                new { request.TeamId });

            if (assignmentCount > 0)
            {
                logger.LogWarning(
                    "Cannot delete team {TeamId} - team has {Count} active shift assignments",
                    request.TeamId,
                    assignmentCount);

                return new DeleteTeamResult(
                    false,
                    $"Không thể xóa team vì đã được phân công {assignmentCount} ca trực. Vui lòng gỡ team khỏi tất cả ca trực trước khi xóa.");
            }

            logger.LogInformation("Team has no active shift assignments - safe to delete");

            // ================================================================
            // BƯỚC 3: SOFT DELETE TEAM (SET IsDeleted = 1)
            // ================================================================
            var now = DateTime.UtcNow;
            team.IsDeleted = true;
            team.DeletedAt = now;
            team.DeletedBy = request.DeletedBy;
            team.UpdatedAt = now;
            team.UpdatedBy = request.DeletedBy;

            await connection.UpdateAsync(team);

            logger.LogInformation(
                "Successfully soft deleted team {TeamId} ({TeamCode})",
                team.Id,
                team.TeamCode);

            // ================================================================
            // BƯỚC 4: DECREMENT TOTALTEAMMANAGED CHO MANAGER
            // ================================================================
            var updateManagerResult = await connection.ExecuteAsync(@"
                UPDATE managers
                SET TotalTeamsManaged = GREATEST(COALESCE(TotalTeamsManaged, 1) - 1, 0)
                WHERE Id = @ManagerId",
                new { team.ManagerId });

            logger.LogInformation(
                "Decremented TotalTeamManaged for Manager {ManagerId}",
                team.ManagerId);

            return new DeleteTeamResult(
                true,
                $"Team {team.TeamCode} đã được xóa thành công");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error soft deleting team {TeamId}", request.TeamId);
            throw;
        }
    }
}
