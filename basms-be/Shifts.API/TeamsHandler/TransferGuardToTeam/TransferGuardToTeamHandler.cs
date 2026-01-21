using Shifts.API.Utilities;

namespace Shifts.API.TeamsHandler.TransferGuardToTeam;

public record TransferGuardToTeamCommand(
    Guid TargetTeamId,
    Guid GuardId,
    string Role,
    string? TransferNotes,
    Guid TransferredBy
) : ICommand<TransferGuardToTeamResult>;

public record TransferGuardToTeamResult
{
    public bool Success { get; init; }
    public Guid NewTeamMemberId { get; init; }
    public string GuardName { get; init; } = string.Empty;
    public string EmployeeCode { get; init; } = string.Empty;
    public PreviousTeamInfo? PreviousTeam { get; init; }
    public NewTeamInfo NewTeam { get; init; } = null!;
}

public record PreviousTeamInfo
{
    public Guid TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}

public record NewTeamInfo
{
    public Guid TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}

internal class TransferGuardToTeamHandler(
    IDbConnectionFactory dbFactory,
    ILogger<TransferGuardToTeamHandler> logger)
    : ICommandHandler<TransferGuardToTeamCommand, TransferGuardToTeamResult>
{
    public async Task<TransferGuardToTeamResult> Handle(
        TransferGuardToTeamCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Transferring guard {GuardId} to team {TargetTeamId}",
                request.GuardId,
                request.TargetTeamId);

            using var connection = await dbFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Validate target team
                var targetTeam = await connection.GetTeamByIdOrThrowAsync(request.TargetTeamId);

                if (!targetTeam.IsActive)
                {
                    throw new InvalidOperationException(
                        $"Team {targetTeam.TeamCode} không active");
                }

                logger.LogInformation(
                    "Target team validated: {TeamCode} - {TeamName}",
                    targetTeam.TeamCode,
                    targetTeam.TeamName);

                // Validate guard
                var guard = await connection.GetGuardByIdOrThrowAsync(request.GuardId);

                if (!guard.IsActive)
                {
                    throw new InvalidOperationException(
                        $"Guard {guard.FullName} không active");
                }

                if (guard.EmploymentStatus != "ACTIVE" && guard.EmploymentStatus != "PROBATION")
                {
                    throw new InvalidOperationException(
                        $"Guard {guard.FullName} có trạng thái làm việc không hợp lệ: {guard.EmploymentStatus}");
                }

                logger.LogInformation(
                    "Guard validated: {FullName} ({EmployeeCode})",
                    guard.FullName,
                    guard.EmployeeCode);

                // Check if guard is already in the target team
                var alreadyInTargetTeam = await connection.QueryFirstOrDefaultAsync<TeamMembers>(
                    @"SELECT * FROM team_members
                      WHERE TeamId = @TargetTeamId
                        AND GuardId = @GuardId
                        AND IsActive = 1
                        AND IsDeleted = 0",
                    new { request.TargetTeamId, request.GuardId },
                    transaction);

                if (alreadyInTargetTeam != null)
                {
                    throw new InvalidOperationException(
                        $"Guard {guard.FullName} đã là thành viên của team {targetTeam.TeamCode}");
                }

                // Find current team membership
                var currentMembership = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT
                        tm.Id AS MembershipId,
                        tm.TeamId,
                        tm.Role,
                        t.TeamCode,
                        t.TeamName,
                        t.CurrentMemberCount
                      FROM team_members tm
                      INNER JOIN teams t ON t.Id = tm.TeamId
                      WHERE tm.GuardId = @GuardId
                        AND tm.IsActive = 1
                        AND tm.IsDeleted = 0
                        AND t.IsActive = 1
                        AND t.IsDeleted = 0",
                    new { request.GuardId },
                    transaction);

                PreviousTeamInfo? previousTeam = null;

                if (currentMembership != null)
                {
                    string currentTeamCode = (string)currentMembership.TeamCode;

                    logger.LogInformation(
                        "Guard {GuardId} is currently in team {TeamCode}, deactivating membership...",
                        request.GuardId,
                        currentTeamCode);

                    previousTeam = new PreviousTeamInfo
                    {
                        TeamId = (Guid)currentMembership.TeamId,
                        TeamCode = currentTeamCode,
                        TeamName = (string)currentMembership.TeamName,
                        Role = (string)currentMembership.Role
                    };

                    // Deactivate current membership
                    await connection.ExecuteAsync(
                        @"UPDATE team_members
                          SET IsActive = 0,
                              LeavingNotes = @LeavingNotes,
                              UpdatedAt = @UpdatedAt,
                              UpdatedBy = @UpdatedBy
                          WHERE Id = @MembershipId",
                        new
                        {
                            MembershipId = currentMembership.MembershipId,
                            LeavingNotes = $"Transferred to team {targetTeam.TeamCode}. {request.TransferNotes ?? ""}",
                            UpdatedAt = DateTime.UtcNow,
                            UpdatedBy = request.TransferredBy
                        },
                        transaction);

                    // Update previous team member count
                    await connection.ExecuteAsync(
                        @"UPDATE teams
                          SET CurrentMemberCount = CurrentMemberCount - 1,
                              UpdatedAt = @UpdatedAt,
                              UpdatedBy = @UpdatedBy
                          WHERE Id = @TeamId",
                        new
                        {
                            TeamId = currentMembership.TeamId,
                            UpdatedAt = DateTime.UtcNow,
                            UpdatedBy = request.TransferredBy
                        },
                        transaction);

                    logger.LogInformation(
                        "Deactivated membership in team {TeamCode}",
                        currentTeamCode);
                }
                else
                {
                    logger.LogInformation(
                        "Guard {GuardId} is not in any active team, proceeding with direct add",
                        request.GuardId);
                }

                // Validate role and certification
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
                        throw new InvalidOperationException(
                            $"Guard {guard.FullName} chưa có CertificationLevel, không thể làm {roleUpper}");
                    }

                    var validLeaderLevels = new[] { "II", "III", "IV", "V", "VI" };
                    if (!validLeaderLevels.Contains(guard.CertificationLevel.ToUpper()))
                    {
                        logger.LogWarning(
                            "Guard {GuardId} has Level {Level} but assigned as {Role}",
                            request.GuardId,
                            guard.CertificationLevel,
                            roleUpper);
                    }
                }

                // Check target team capacity
                if (targetTeam.MaxMembers.HasValue && targetTeam.CurrentMemberCount >= targetTeam.MaxMembers.Value)
                {
                    throw new InvalidOperationException(
                        $"Team {targetTeam.TeamCode} đã đạt giới hạn tối đa {targetTeam.MaxMembers.Value} thành viên");
                }

                // Create new membership
                var newMembership = new TeamMembers
                {
                    Id = Guid.NewGuid(),
                    TeamId = request.TargetTeamId,
                    GuardId = request.GuardId,
                    Role = roleUpper,
                    IsActive = true,
                    JoiningNotes = $"Transferred from {previousTeam?.TeamCode ?? "N/A"}. {request.TransferNotes ?? ""}",
                    TotalShiftsAssigned = 0,
                    TotalShiftsCompleted = 0,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.TransferredBy,
                    IsDeleted = false
                };

                await connection.InsertAsync(newMembership, transaction);

                logger.LogInformation(
                    "Created new membership {MembershipId} in team {TeamCode}",
                    newMembership.Id,
                    targetTeam.TeamCode);

                // Update target team member count
                await connection.ExecuteAsync(
                    @"UPDATE teams
                      SET CurrentMemberCount = CurrentMemberCount + 1,
                          UpdatedAt = @UpdatedAt,
                          UpdatedBy = @UpdatedBy
                      WHERE Id = @TeamId",
                    new
                    {
                        TeamId = request.TargetTeamId,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = request.TransferredBy
                    },
                    transaction);

                transaction.Commit();

                logger.LogInformation(
                    "Successfully transferred guard {GuardName} from team {PreviousTeam} to team {NewTeam}",
                    guard.FullName,
                    previousTeam?.TeamCode ?? "N/A",
                    targetTeam.TeamCode);

                return new TransferGuardToTeamResult
                {
                    Success = true,
                    NewTeamMemberId = newMembership.Id,
                    GuardName = guard.FullName,
                    EmployeeCode = guard.EmployeeCode,
                    PreviousTeam = previousTeam,
                    NewTeam = new NewTeamInfo
                    {
                        TeamId = targetTeam.Id,
                        TeamCode = targetTeam.TeamCode,
                        TeamName = targetTeam.TeamName,
                        Role = roleUpper
                    }
                };
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error transferring guard to team");
            throw;
        }
    }
}
