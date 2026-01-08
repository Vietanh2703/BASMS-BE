using Shifts.API.Utilities;

namespace Shifts.API.TeamsHandler.CreateTeam;

public record CreateTeamCommand(
    Guid ManagerId,
    string TeamName,
    string? Specialization,
    string? Description,
    int MinMembers,
    int? MaxMembers,
    Guid CreatedBy
) : ICommand<CreateTeamResult>;

public record CreateTeamResult(
    Guid TeamId,
    string TeamCode,
    string TeamName
);

internal class CreateTeamHandler(
    IDbConnectionFactory dbFactory,
    ILogger<CreateTeamHandler> logger)
    : ICommandHandler<CreateTeamCommand, CreateTeamResult>
{
    public async Task<CreateTeamResult> Handle(
        CreateTeamCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Creating team '{TeamName}' for Manager {ManagerId}",
                request.TeamName,
                request.ManagerId);

            using var connection = await dbFactory.CreateConnectionAsync();

            logger.LogInformation("Validating manager {ManagerId}", request.ManagerId);

            var manager = await connection.GetManagerByIdOrThrowAsync(request.ManagerId);

            logger.LogInformation(
                "Manager validated: {FullName} ({EmployeeCode})",
                manager.FullName,
                manager.EmployeeCode);
            
            if (string.IsNullOrWhiteSpace(request.TeamName))
            {
                throw new InvalidOperationException("Tên team không được để trống");
            }

            if (request.MinMembers < 1)
            {
                throw new InvalidOperationException("MinMembers phải >= 1");
            }

            if (request.MaxMembers.HasValue && request.MaxMembers.Value < request.MinMembers)
            {
                throw new InvalidOperationException(
                    "MaxMembers phải >= MinMembers");
            }
            
            var validSpecializations = new[] { "RESIDENTIAL", "COMMERCIAL", "EVENT", "VIP", "INDUSTRIAL" };
            if (!string.IsNullOrWhiteSpace(request.Specialization) &&
                !validSpecializations.Contains(request.Specialization.ToUpper()))
            {
                throw new InvalidOperationException(
                    $"Specialization không hợp lệ. Phải là một trong: {string.Join(", ", validSpecializations)}");
            }

            logger.LogInformation("Input validation passed");
            
            var teamCode = await GenerateUniqueTeamCodeAsync(connection);

            logger.LogInformation("Generated team code: {TeamCode}", teamCode);
            
            var team = new Teams
            {
                Id = Guid.NewGuid(),
                ManagerId = request.ManagerId,
                TeamCode = teamCode,
                TeamName = request.TeamName.Trim(),
                Description = request.Description?.Trim(),
                Specialization = request.Specialization?.ToUpper(),
                MinMembers = request.MinMembers,
                MaxMembers = request.MaxMembers,
                CurrentMemberCount = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.CreatedBy,
                IsDeleted = false
            };

            await connection.InsertAsync(team);

            logger.LogInformation(
                "Successfully created team {TeamId} with code {TeamCode}",
                team.Id,
                team.TeamCode);

            var updateResult = await connection.ExecuteAsync(@"
                UPDATE managers
                SET TotalTeamsManaged = COALESCE(TotalTeamsManaged, 0) + 1
                WHERE Id = @ManagerId",
                new { request.ManagerId });

            logger.LogInformation(
                "Incremented TotalTeamManaged for Manager {ManagerId}",
                request.ManagerId);

            return new CreateTeamResult(
                team.Id,
                team.TeamCode,
                team.TeamName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating team");
            throw;
        }
    }

    private async Task<string> GenerateUniqueTeamCodeAsync(IDbConnection connection)
    {
        const int maxAttempts = 10;
        var random = new Random();

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {

            var randomNumber = random.Next(100000, 999999); 
            var teamCode = $"T-{randomNumber}";
            var exists = await connection.QueryFirstOrDefaultAsync<bool>(
                "SELECT COUNT(1) FROM teams WHERE TeamCode = @TeamCode AND IsDeleted = 0",
                new { TeamCode = teamCode });

            if (!exists)
            {
                return teamCode;
            }

            logger.LogWarning(
                "Team code {TeamCode} already exists, retrying... (attempt {Attempt}/{MaxAttempts})",
                teamCode,
                attempt + 1,
                maxAttempts);
        }

        throw new InvalidOperationException(
            $"Failed to generate unique team code after {maxAttempts} attempts");
    }
}
