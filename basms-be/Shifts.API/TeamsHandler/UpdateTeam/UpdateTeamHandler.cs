namespace Shifts.API.TeamsHandler.UpdateTeam;

public record UpdateTeamCommand(
    Guid TeamId,
    string? TeamName,
    string? Specialization,
    string? Description,
    int? MinMembers,
    int? MaxMembers,
    Guid UpdatedBy
) : ICommand<UpdateTeamResult>;

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

            var team = await connection.GetTeamByIdOrThrowAsync(request.TeamId);

            logger.LogInformation(
                "Found team {TeamId}: {TeamCode} - {TeamName}",
                team.Id,
                team.TeamCode,
                team.TeamName);
            
            if (!string.IsNullOrWhiteSpace(request.TeamName) && string.IsNullOrWhiteSpace(request.TeamName.Trim()))
            {
                throw new InvalidOperationException("Tên team không được để trống");
            }

            if (request.MinMembers.HasValue && request.MinMembers.Value < 1)
            {
                throw new InvalidOperationException("MinMembers phải >= 1");
            }

            var finalMinMembers = request.MinMembers ?? team.MinMembers;
            if (request.MaxMembers.HasValue && request.MaxMembers.Value < finalMinMembers)
            {
                throw new InvalidOperationException(
                    $"MaxMembers ({request.MaxMembers.Value}) phải >= MinMembers ({finalMinMembers})");
            }
            
            if (request.MaxMembers.HasValue && request.MaxMembers.Value < team.CurrentMemberCount)
            {
                throw new InvalidOperationException(
                    $"MaxMembers ({request.MaxMembers.Value}) không thể nhỏ hơn số thành viên hiện tại ({team.CurrentMemberCount})");
            }
            
            if (!string.IsNullOrWhiteSpace(request.Specialization))
            {
                var validSpecializations = new[] { "RESIDENTIAL", "COMMERCIAL", "EVENT", "VIP", "INDUSTRIAL" };
                if (!validSpecializations.Contains(request.Specialization.ToUpper()))
                {
                    throw new InvalidOperationException(
                        $"Specialization không hợp lệ. Phải là một trong: {string.Join(", ", validSpecializations)}");
                }
            }

            logger.LogInformation("Input validation passed");
            
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

            if (!hasChanges)
            {
                logger.LogInformation("No changes to update for team {TeamId}", team.Id);
                return new UpdateTeamResult(true, "No changes detected");
            }
            
            team.UpdatedAt = DateTime.UtcNow;
            team.UpdatedBy = request.UpdatedBy;

            await connection.UpdateAsync(team);

            logger.LogInformation(
                "Successfully updated team {TeamId} ({TeamCode})",
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
