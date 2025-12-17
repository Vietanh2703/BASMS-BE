using Shifts.API.GuardsHandler.GetGuardById;

namespace Shifts.API.GuardsHandler.GetAllGuardLevelIIAndIII;

/// <summary>
/// Query để lấy tất cả guards có CertificationLevel II và III
/// </summary>
public record GetAllGuardLevelIIAndIIIQuery : IQuery<GetAllGuardLevelIIAndIIIResult>;

/// <summary>
/// Result chứa danh sách guards Level II và III
/// </summary>
public record GetAllGuardLevelIIAndIIIResult(
    int TotalGuards,
    int LevelIICount,
    int LevelIIICount,
    List<GuardDetailDto> Guards
);

/// <summary>
/// Handler để lấy danh sách guards có CertificationLevel II và III
/// </summary>
internal class GetAllGuardLevelIIAndIIIHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetAllGuardLevelIIAndIIIHandler> logger)
    : IQueryHandler<GetAllGuardLevelIIAndIIIQuery, GetAllGuardLevelIIAndIIIResult>
{
    public async Task<GetAllGuardLevelIIAndIIIResult> Handle(
        GetAllGuardLevelIIAndIIIQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting all guards with CertificationLevel II and III");

            using var connection = await connectionFactory.CreateConnectionAsync();

            // Lấy tất cả guards có CertificationLevel = "II" hoặc "III"
            var allGuards = await connection.GetAllAsync<Guards>();
            var levelIIAndIIIGuards = allGuards
                .Where(g => (g.CertificationLevel == "II" || g.CertificationLevel == "III")
                            && !g.IsDeleted
                            && g.IsActive)
                .OrderBy(g => g.CertificationLevel) // Level II trước, sau đó Level III
                .ThenBy(g => g.EmployeeCode)
                .ToList();

            // Đếm số lượng theo từng level
            int levelIICount = levelIIAndIIIGuards.Count(g => g.CertificationLevel == "II");
            int levelIIICount = levelIIAndIIIGuards.Count(g => g.CertificationLevel == "III");

            logger.LogInformation(
                "Found {TotalCount} guards: {LevelIICount} Level II, {LevelIIICount} Level III",
                levelIIAndIIIGuards.Count,
                levelIICount,
                levelIIICount);

            // Map entities sang DTOs
            var guardDtos = levelIIAndIIIGuards.Select(guard => new GuardDetailDto(
                Id: guard.Id,
                IdentityNumber: guard.IdentityNumber,
                EmployeeCode: guard.EmployeeCode,
                FullName: guard.FullName,
                Email: guard.Email ?? string.Empty,
                AvatarUrl: guard.AvatarUrl,
                PhoneNumber: guard.PhoneNumber,
                DateOfBirth: guard.DateOfBirth,
                Gender: guard.Gender,
                CurrentAddress: guard.CurrentAddress,
                EmploymentStatus: guard.EmploymentStatus,
                HireDate: guard.HireDate,
                ContractType: guard.ContractType,
                CertificationLevel: guard.CertificationLevel,
                TerminationDate: guard.TerminationDate,
                TerminationReason: guard.TerminationReason,
                MaxWeeklyHours: guard.MaxWeeklyHours,
                CanWorkOvertime: guard.CanWorkOvertime,
                CanWorkWeekends: guard.CanWorkWeekends,
                CanWorkHolidays: guard.CanWorkHolidays,
                CurrentAvailability: guard.CurrentAvailability,
                IsActive: guard.IsActive,
                LastSyncedAt: guard.LastSyncedAt,
                SyncStatus: guard.SyncStatus,
                UserServiceVersion: guard.UserServiceVersion,
                CreatedAt: guard.CreatedAt,
                UpdatedAt: guard.UpdatedAt
            )).ToList();

            logger.LogInformation(
                "Successfully retrieved {Count} Level II and III guards",
                guardDtos.Count);

            return new GetAllGuardLevelIIAndIIIResult(
                TotalGuards: guardDtos.Count,
                LevelIICount: levelIICount,
                LevelIIICount: levelIIICount,
                Guards: guardDtos
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting guards with CertificationLevel II and III");
            throw;
        }
    }
}
