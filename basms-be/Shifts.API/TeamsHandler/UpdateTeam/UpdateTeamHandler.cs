using Dapper.Contrib.Extensions;
using Shifts.API.Data;
using Shifts.API.Models;

namespace Shifts.API.TeamsHandler.UpdateTeam;

/// <summary>
/// Command để update team
/// </summary>
public record UpdateTeamCommand(
    Guid TeamId,
    string? TeamName,           // Optional - chỉ update nếu khác null
    string? Specialization,
    string? Description,
    int? MinMembers,
    int? MaxMembers,
    bool? IsActive,
    Guid UpdatedBy
) : ICommand<UpdateTeamResult>;

/// <summary>
/// Result
/// </summary>
public record UpdateTeamResult(bool Success, string Message);

internal class UpdateTeamHandler(
    IDbConnectionFactory dbFactory,
    ILogger<UpdateTeamHandler> logger)
    : ICommandHandler<UpdateTeamCommand, UpdateTeamResult>
{
    public async Task<UpdateTeamResult> Handle(
        UpdateTeamCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Updating team {TeamId}", request.TeamId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: LẤY TEAM HIỆN TẠI
            // ================================================================
            var team = await connection.GetAsync<Teams>(request.TeamId);

            if (team == null || team.IsDeleted)
            {
                logger.LogWarning("Team {TeamId} not found", request.TeamId);
                throw new InvalidOperationException($"Team {request.TeamId} không tồn tại");
            }

            logger.LogInformation(
                "Found team {TeamId}: {TeamCode} - {TeamName}",
                team.Id,
                team.TeamCode,
                team.TeamName);

            // ================================================================
            // BƯỚC 2: VALIDATE INPUT
            // ================================================================
            if (!string.IsNullOrWhiteSpace(request.TeamName) && string.IsNullOrWhiteSpace(request.TeamName.Trim()))
            {
                throw new InvalidOperationException("Tên team không được để trống");
            }

            if (request.MinMembers.HasValue && request.MinMembers.Value < 1)
            {
                throw new InvalidOperationException("MinMembers phải >= 1");
            }

            // Validate MaxMembers with MinMembers
            var finalMinMembers = request.MinMembers ?? team.MinMembers;
            if (request.MaxMembers.HasValue && request.MaxMembers.Value < finalMinMembers)
            {
                throw new InvalidOperationException(
                    $"MaxMembers ({request.MaxMembers.Value}) phải >= MinMembers ({finalMinMembers})");
            }

            // Validate MaxMembers with current member count
            if (request.MaxMembers.HasValue && request.MaxMembers.Value < team.CurrentMemberCount)
            {
                throw new InvalidOperationException(
                    $"MaxMembers ({request.MaxMembers.Value}) không thể nhỏ hơn số thành viên hiện tại ({team.CurrentMemberCount})");
            }

            // Validate specialization nếu có
            if (!string.IsNullOrWhiteSpace(request.Specialization))
            {
                var validSpecializations = new[] { "RESIDENTIAL", "COMMERCIAL", "EVENT", "VIP", "INDUSTRIAL" };
                if (!validSpecializations.Contains(request.Specialization.ToUpper()))
                {
                    throw new InvalidOperationException(
                        $"Specialization không hợp lệ. Phải là một trong: {string.Join(", ", validSpecializations)}");
                }
            }

            logger.LogInformation("✓ Input validation passed");

            // ================================================================
            // BƯỚC 3: UPDATE CÁC FIELDS
            // ================================================================
            bool hasChanges = false;
            var changesList = new List<string>();

            if (!string.IsNullOrWhiteSpace(request.TeamName))
            {
                changesList.Add($"Tên team: '{team.TeamName}' → '{request.TeamName}'");
                team.TeamName = request.TeamName.Trim();
                hasChanges = true;
            }

            if (!string.IsNullOrWhiteSpace(request.Specialization))
            {
                changesList.Add($"Chuyên môn: {team.Specialization ?? "N/A"} → {request.Specialization.ToUpper()}");
                team.Specialization = request.Specialization.ToUpper();
                hasChanges = true;
            }

            if (request.Description != null)
            {
                team.Description = request.Description.Trim();
                hasChanges = true;
                changesList.Add("Mô tả đã được cập nhật");
            }

            if (request.MinMembers.HasValue)
            {
                changesList.Add($"MinMembers: {team.MinMembers} → {request.MinMembers.Value}");
                team.MinMembers = request.MinMembers.Value;
                hasChanges = true;
            }

            if (request.MaxMembers.HasValue)
            {
                changesList.Add($"MaxMembers: {team.MaxMembers?.ToString() ?? "unlimited"} → {request.MaxMembers.Value}");
                team.MaxMembers = request.MaxMembers.Value;
                hasChanges = true;
            }

            if (request.IsActive.HasValue)
            {
                changesList.Add($"IsActive: {team.IsActive} → {request.IsActive.Value}");
                team.IsActive = request.IsActive.Value;
                hasChanges = true;
            }

            if (!hasChanges)
            {
                logger.LogInformation("No changes to update for team {TeamId}", team.Id);
                return new UpdateTeamResult(true, "No changes detected");
            }

            // ================================================================
            // BƯỚC 4: SAVE CHANGES
            // ================================================================
            team.UpdatedAt = DateTime.UtcNow;
            team.UpdatedBy = request.UpdatedBy;

            await connection.UpdateAsync(team);

            logger.LogInformation(
                "✓ Successfully updated team {TeamId} ({TeamCode})",
                team.Id,
                team.TeamCode);

            var changesDescription = string.Join(", ", changesList);
            logger.LogInformation("Changes: {Changes}", changesDescription);

            return new UpdateTeamResult(
                true,
                $"Team '{team.TeamName}' được cập nhật thành công. {changesDescription}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating team {TeamId}", request.TeamId);
            throw;
        }
    }
}
