using Dapper;
using Dapper.Contrib.Extensions;
using Shifts.API.Data;
using Shifts.API.Models;

namespace Shifts.API.TeamsHandler.CreateTeam;

/// <summary>
/// Command để tạo team mới
/// </summary>
public record CreateTeamCommand(
    Guid ManagerId,                 // Manager quản lý team
    string TeamName,                // Tên team: "Đội Bảo Vệ Khu A"
    string? Specialization,         // RESIDENTIAL | COMMERCIAL | EVENT | VIP | INDUSTRIAL
    string? Description,            // Mô tả team
    int MinMembers,                 // Số guards tối thiểu: 1
    int? MaxMembers,                // Số guards tối đa: 10 (nullable)
    Guid CreatedBy                  // Manager tạo team
) : ICommand<CreateTeamResult>;

/// <summary>
/// Result chứa team đã tạo
/// </summary>
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

            // ================================================================
            // BƯỚC 1: VALIDATE MANAGER
            // ================================================================
            logger.LogInformation("Validating manager {ManagerId}", request.ManagerId);

            var manager = await connection.QueryFirstOrDefaultAsync<Managers>(
                @"SELECT * FROM managers
                  WHERE Id = @ManagerId
                    AND IsDeleted = 0
                    AND IsActive = 1",
                new { request.ManagerId });

            if (manager == null)
            {
                logger.LogWarning("Manager {ManagerId} not found or inactive", request.ManagerId);
                throw new InvalidOperationException(
                    $"Manager {request.ManagerId} không tồn tại hoặc không active");
            }

            logger.LogInformation(
                "✓ Manager validated: {FullName} ({EmployeeCode})",
                manager.FullName,
                manager.EmployeeCode);

            // ================================================================
            // BƯỚC 2: VALIDATE INPUT
            // ================================================================
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

            // Validate specialization nếu có
            var validSpecializations = new[] { "RESIDENTIAL", "COMMERCIAL", "EVENT", "VIP", "INDUSTRIAL" };
            if (!string.IsNullOrWhiteSpace(request.Specialization) &&
                !validSpecializations.Contains(request.Specialization.ToUpper()))
            {
                throw new InvalidOperationException(
                    $"Specialization không hợp lệ. Phải là một trong: {string.Join(", ", validSpecializations)}");
            }

            logger.LogInformation("✓ Input validation passed");

            // ================================================================
            // BƯỚC 3: GENERATE UNIQUE TEAM CODE (T-xxxxxx)
            // ================================================================
            var teamCode = await GenerateUniqueTeamCodeAsync(connection);

            logger.LogInformation("✓ Generated team code: {TeamCode}", teamCode);

            // ================================================================
            // BƯỚC 4: CREATE TEAM
            // ================================================================
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
                "✓ Successfully created team {TeamId} with code {TeamCode}",
                team.Id,
                team.TeamCode);

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

    /// <summary>
    /// Generate unique team code: T-xxxxxx (T- + 6 random digits)
    /// </summary>
    private async Task<string> GenerateUniqueTeamCodeAsync(IDbConnection connection)
    {
        const int maxAttempts = 10;
        var random = new Random();

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // Generate 6 random digits
            var randomNumber = random.Next(100000, 999999); // 100000 to 999999
            var teamCode = $"T-{randomNumber}";

            // Check if code already exists
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
