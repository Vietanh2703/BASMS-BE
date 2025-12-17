using Shifts.API.GuardsHandler.GetGuardById;

namespace Shifts.API.GuardsHandler.GetAllGuardLevelI;

/// <summary>
/// Query để lấy tất cả guards có CertificationLevel I
/// </summary>
public record GetAllGuardLevelIQuery : IQuery<GetAllGuardLevelIResult>;

/// <summary>
/// Result chứa danh sách guards Level I
/// </summary>
public record GetAllGuardLevelIResult(
    int TotalGuards,
    List<GuardDetailDto> Guards
);

/// <summary>
/// Handler để lấy danh sách guards có CertificationLevel I
/// </summary>
internal class GetAllGuardLevelIHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetAllGuardLevelIHandler> logger)
    : IQueryHandler<GetAllGuardLevelIQuery, GetAllGuardLevelIResult>
{
    public async Task<GetAllGuardLevelIResult> Handle(
        GetAllGuardLevelIQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting all guards with CertificationLevel I");

            using var connection = await connectionFactory.CreateConnectionAsync();

            // Lấy tất cả guards có CertificationLevel = "I"
            var allGuards = await connection.GetAllAsync<Guards>();
            var levelIGuards = allGuards
                .Where(g => g.CertificationLevel == "I" && !g.IsDeleted && g.IsActive)
                .OrderBy(g => g.EmployeeCode)
                .ToList();

            logger.LogInformation(
                "Found {Count} guards with CertificationLevel I",
                levelIGuards.Count);

            // Map entities sang DTOs
            var guardDtos = levelIGuards.Select(guard => new GuardDetailDto(
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
                "Successfully retrieved {Count} Level I guards",
                guardDtos.Count);

            return new GetAllGuardLevelIResult(
                TotalGuards: guardDtos.Count,
                Guards: guardDtos
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting guards with CertificationLevel I");
            throw;
        }
    }
}
