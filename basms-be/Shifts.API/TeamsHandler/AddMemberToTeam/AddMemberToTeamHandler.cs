using Shifts.API.Utilities;

namespace Shifts.API.TeamsHandler.AddMemberToTeam;

public record AddMemberToTeamCommand(
    Guid TeamId,
    Guid GuardId,
    string Role,
    string? JoiningNotes,
    Guid CreatedBy
) : ICommand<AddMemberToTeamResult>;

public record AddMemberToTeamResult(
    Guid TeamMemberId,
    Guid TeamId,
    string TeamCode,
    Guid GuardId,
    string GuardName,
    string Role
);

internal class AddMemberToTeamHandler(
    IDbConnectionFactory dbFactory,
    ILogger<AddMemberToTeamHandler> logger)
    : ICommandHandler<AddMemberToTeamCommand, AddMemberToTeamResult>
{
    public async Task<AddMemberToTeamResult> Handle(
        AddMemberToTeamCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Adding guard {GuardId} to team {TeamId} with role {Role}",
                request.GuardId,
                request.TeamId,
                request.Role);

            using var connection = await dbFactory.CreateConnectionAsync();

            logger.LogInformation("Validating team {TeamId}", request.TeamId);

            var team = await connection.GetTeamByIdOrThrowAsync(request.TeamId);

            if (!team.IsActive)
            {
                logger.LogWarning("Team {TeamId} is not active", request.TeamId);
                throw new InvalidOperationException(
                    $"Team {team.TeamCode} không active");
            }

            logger.LogInformation(
                "Team validated: {TeamCode} - {TeamName}",
                team.TeamCode,
                team.TeamName);
            
            if (team.MaxMembers.HasValue && team.CurrentMemberCount >= team.MaxMembers.Value)
            {
                logger.LogWarning(
                    "Team {TeamCode} has reached max members limit ({MaxMembers})",
                    team.TeamCode,
                    team.MaxMembers.Value);
                throw new InvalidOperationException(
                    $"Team {team.TeamCode} đã đạt giới hạn tối đa {team.MaxMembers.Value} thành viên");
            }

            logger.LogInformation(
                "Team has capacity: {Current}/{Max} members",
                team.CurrentMemberCount,
                team.MaxMembers?.ToString() ?? "unlimited");

            logger.LogInformation("Validating guard {GuardId}", request.GuardId);

            var guard = await connection.GetGuardByIdOrThrowAsync(request.GuardId);

            if (!guard.IsActive)
            {
                logger.LogWarning("Guard {GuardId} is not active", request.GuardId);
                throw new InvalidOperationException(
                    $"Guard {guard.FullName} không active");
            }

            if (guard.EmploymentStatus != "ACTIVE" && guard.EmploymentStatus != "PROBATION")
            {
                logger.LogWarning(
                    "Guard {GuardId} has invalid employment status: {Status}",
                    request.GuardId,
                    guard.EmploymentStatus);
                throw new InvalidOperationException(
                    $"Guard {guard.FullName} có trạng thái làm việc không hợp lệ: {guard.EmploymentStatus}");
            }

            logger.LogInformation(
                "Guard validated: {FullName} ({EmployeeCode}) - Level {Level}",
                guard.FullName,
                guard.EmployeeCode,
                guard.CertificationLevel ?? "N/A");

            // Check if guard is already in another active team
            logger.LogInformation("Checking if guard {GuardId} belongs to any other active team", request.GuardId);

            var existingTeamMembership = await connection.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT
                    tm.Id AS MembershipId,
                    tm.TeamId,
                    tm.Role,
                    t.TeamCode,
                    t.TeamName
                  FROM team_members tm
                  INNER JOIN teams t ON t.Id = tm.TeamId
                  WHERE tm.GuardId = @GuardId
                    AND tm.IsActive = 1
                    AND tm.IsDeleted = 0
                    AND t.IsActive = 1
                    AND t.IsDeleted = 0
                    AND tm.TeamId != @TeamId",
                new { request.GuardId, request.TeamId });

            if (existingTeamMembership != null)
            {
                string teamCode = (string)existingTeamMembership.TeamCode;
                string teamName = (string)existingTeamMembership.TeamName;
                string role = (string)existingTeamMembership.Role;

                logger.LogWarning(
                    "Guard {GuardId} is already a member of team {TeamCode} ({TeamName}) with role {Role}",
                    request.GuardId,
                    teamCode,
                    teamName,
                    role);

                throw new InvalidOperationException(
                    $"Guard {guard.FullName} ({guard.EmployeeCode}) đã là thành viên của team {teamCode} - {teamName} " +
                    $"với vai trò {role}. " +
                    $"Vui lòng remove khỏi team đó trước khi thêm vào team mới.");
            }

            logger.LogInformation("Guard {GuardId} is not in any other active team", request.GuardId);

            if (team.MaxMembers.HasValue && team.MaxMembers.Value == 1)
            {
                logger.LogInformation(
                    "Team {TeamCode} is a single-guard team, checking certification level...",
                    team.TeamCode);

                if (string.IsNullOrWhiteSpace(guard.CertificationLevel))
                {
                    logger.LogWarning(
                        "Guard {GuardId} has no certification level for single-guard team {TeamId}",
                        request.GuardId,
                        request.TeamId);
                    throw new InvalidOperationException(
                        $"Team {team.TeamCode} chỉ có 1 người, guard {guard.FullName} phải có cấp bậc II hoặc III");
                }

                var validSingleGuardLevels = new[] { "II", "III" };
                if (!validSingleGuardLevels.Contains(guard.CertificationLevel.ToUpper()))
                {
                    logger.LogWarning(
                        "Guard {GuardId} has Level {Level} but team {TeamId} requires Level II or III",
                        request.GuardId,
                        guard.CertificationLevel,
                        request.TeamId);
                    throw new InvalidOperationException(
                        $"Team {team.TeamCode} chỉ có 1 người, guard {guard.FullName} phải có cấp bậc II hoặc III. " +
                        $"Hiện tại guard có cấp bậc {guard.CertificationLevel}");
                }

                logger.LogInformation(
                    "Guard {FullName} with Level {Level} is qualified for single-guard team",
                    guard.FullName,
                    guard.CertificationLevel);
            }
            
            var existingMembership = await connection.QueryFirstOrDefaultAsync<TeamMembers>(
                @"SELECT * FROM team_members
                  WHERE TeamId = @TeamId
                    AND GuardId = @GuardId
                    AND IsDeleted = 0",
                new { request.TeamId, request.GuardId });

            if (existingMembership != null)
            {
                if (existingMembership.IsActive)
                {
                    logger.LogWarning(
                        "Guard {GuardId} is already an active member of team {TeamId}",
                        request.GuardId,
                        request.TeamId);
                    throw new InvalidOperationException(
                        $"Guard {guard.FullName} đã là thành viên của team {team.TeamCode}");
                }
                else
                {
                    logger.LogInformation(
                        "Guard {GuardId} was previously in team {TeamId} but is inactive, checking before reactivation...",
                        request.GuardId,
                        request.TeamId);

                    // CRITICAL FIX: Check if guard is currently active in another team before reactivation
                    var currentActiveTeam = await connection.QueryFirstOrDefaultAsync<dynamic>(
                        @"SELECT tm.TeamId, t.TeamCode, t.TeamName, tm.Role
                          FROM team_members tm
                          INNER JOIN teams t ON t.Id = tm.TeamId
                          WHERE tm.GuardId = @GuardId
                            AND tm.IsActive = 1 AND tm.IsDeleted = 0
                            AND t.IsActive = 1 AND t.IsDeleted = 0
                            AND tm.TeamId != @TeamId",
                        new { request.GuardId, request.TeamId });

                    if (currentActiveTeam != null)
                    {
                        string currentTeamCode = (string)currentActiveTeam.TeamCode;
                        string currentTeamName = (string)currentActiveTeam.TeamName;
                        string currentRole = (string)currentActiveTeam.Role;

                        logger.LogWarning(
                            "Cannot reactivate - guard {GuardId} is active in team {CurrentTeamCode}",
                            request.GuardId, currentTeamCode);

                        throw new InvalidOperationException(
                            $"Không thể reactivate guard {guard.FullName} trong team {team.TeamCode}. " +
                            $"Guard đang active ở team {currentTeamCode} - {currentTeamName} với vai trò {currentRole}. " +
                            $"Vui lòng remove khỏi team đó trước hoặc dùng Transfer API.");
                    }

                    logger.LogInformation("Guard {GuardId} not in other active team, reactivating", request.GuardId);

                    existingMembership.IsActive = true;
                    existingMembership.Role = request.Role.ToUpper();
                    existingMembership.JoiningNotes = request.JoiningNotes;
                    existingMembership.UpdatedAt = DateTime.UtcNow;
                    existingMembership.UpdatedBy = request.CreatedBy;

                    await connection.UpdateAsync(existingMembership);
                    
                    team.CurrentMemberCount++;
                    team.UpdatedAt = DateTime.UtcNow;
                    team.UpdatedBy = request.CreatedBy;
                    await connection.UpdateAsync(team);

                    logger.LogInformation(
                        "Reactivated guard {GuardId} in team {TeamId}",
                        request.GuardId,
                        request.TeamId);

                    return new AddMemberToTeamResult(
                        existingMembership.Id,
                        team.Id,
                        team.TeamCode,
                        guard.Id,
                        guard.FullName,
                        existingMembership.Role);
                }
            }
            
            var validRoles = new[] { "LEADER", "DEPUTY", "MEMBER" };
            var roleUpper = request.Role.ToUpper();

            if (!validRoles.Contains(roleUpper))
            {
                throw new InvalidOperationException(
                    $"Role không hợp lệ. Phải là một trong: {string.Join(", ", validRoles)}");
            }
            if (roleUpper == "LEADER" || roleUpper == "DEPUTY")
            {
                if (string.IsNullOrWhiteSpace(guard.CertificationLevel))
                {
                    logger.LogWarning(
                        "Guard {GuardId} has no certification level but assigned as {Role}",
                        request.GuardId,
                        roleUpper);
                    throw new InvalidOperationException(
                        $"Guard {guard.FullName} chưa có CertificationLevel, không thể làm {roleUpper}");
                }
                
                var validLeaderLevels = new[] { "II", "III", "IV", "V", "VI" };
                if (!validLeaderLevels.Contains(guard.CertificationLevel.ToUpper()))
                {
                    logger.LogWarning(
                        "Guard {GuardId} has Level {Level} but assigned as {Role}. Recommending Level II or III.",
                        request.GuardId,
                        guard.CertificationLevel,
                        roleUpper);
                    
                    logger.LogInformation(
                        "Warning: {Role} {GuardName} có CertificationLevel {Level}. " +
                        "Khuyến nghị Level II hoặc III cho vị trí này.",
                        roleUpper,
                        guard.FullName,
                        guard.CertificationLevel);
                }
            }

            logger.LogInformation("Role and certification validation passed");

            var teamMember = new TeamMembers
            {
                Id = Guid.NewGuid(),
                TeamId = request.TeamId,
                GuardId = request.GuardId,
                Role = roleUpper,
                IsActive = true,
                JoiningNotes = request.JoiningNotes,
                TotalShiftsAssigned = 0,
                TotalShiftsCompleted = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.CreatedBy,
                IsDeleted = false
            };

            await connection.InsertAsync(teamMember);

            logger.LogInformation(
                "Team member created: {TeamMemberId}",
                teamMember.Id);

            team.CurrentMemberCount++;
            team.UpdatedAt = DateTime.UtcNow;
            team.UpdatedBy = request.CreatedBy;
            await connection.UpdateAsync(team);

            logger.LogInformation(
                "Team {TeamCode} member count updated: {Count}",
                team.TeamCode,
                team.CurrentMemberCount);
            
            logger.LogInformation(
                "Successfully added guard {GuardName} to team {TeamCode} as {Role}",
                guard.FullName,
                team.TeamCode,
                roleUpper);

            return new AddMemberToTeamResult(
                teamMember.Id,
                team.Id,
                team.TeamCode,
                guard.Id,
                guard.FullName,
                roleUpper);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding member to team");
            throw;
        }
    }
}
