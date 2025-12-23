namespace Shifts.API.GuardsHandler.GetAllGuards;

public record GetAllGuardsQuery() : IQuery<GetAllGuardsResult>;
public record GetAllGuardsResult(IEnumerable<GuardDto> Guards);

public record GuardDto(
    Guid Id,
    string IdentityNumber,
    string EmployeeCode,
    string FullName,
    string Email,
    string? AvatarUrl,
    string? PhoneNumber,
    DateTime? DateOfBirth,
    string? Gender,
    string? CurrentAddress,
    string EmploymentStatus,
    DateTime? HireDate,
    string? ContractType,
    int MaxWeeklyHours,
    bool CanWorkOvertime,
    bool CanWorkWeekends,
    bool CanWorkHolidays,
    string CurrentAvailability,
    Guid? DirectManagerId,
    bool IsActive,
    DateTime? LastSyncedAt,
    string SyncStatus,
    DateTime CreatedAt
);

internal class GetAllGuardsHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetAllGuardsHandler> logger)
    : IQueryHandler<GetAllGuardsQuery, GetAllGuardsResult>
{
    public async Task<GetAllGuardsResult> Handle(GetAllGuardsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting all guards from cache database");

            using var connection = await connectionFactory.CreateConnectionAsync();
            
            var guards = await connection.GetAllAsync<Guards>();
            var guardDtos = guards
                .Where(g => !g.IsDeleted && g.IsActive)
                .Select(g => new GuardDto(
                    Id: g.Id,
                    IdentityNumber: g.IdentityNumber,
                    EmployeeCode: g.EmployeeCode,
                    FullName: g.FullName,
                    Email: g.Email ?? string.Empty,
                    AvatarUrl: g.AvatarUrl,
                    PhoneNumber: g.PhoneNumber,
                    DateOfBirth: g.DateOfBirth,
                    Gender: g.Gender,
                    CurrentAddress: g.CurrentAddress,
                    EmploymentStatus: g.EmploymentStatus,
                    HireDate: g.HireDate,
                    ContractType: g.ContractType,
                    MaxWeeklyHours: g.MaxWeeklyHours,
                    CanWorkOvertime: g.CanWorkOvertime,
                    CanWorkWeekends: g.CanWorkWeekends,
                    CanWorkHolidays: g.CanWorkHolidays,
                    CurrentAvailability: g.CurrentAvailability,
                    DirectManagerId: g.DirectManagerId,
                    IsActive: g.IsActive,
                    LastSyncedAt: g.LastSyncedAt,
                    SyncStatus: g.SyncStatus,
                    CreatedAt: g.CreatedAt
                ))
                .OrderByDescending(g => g.CreatedAt)
                .ToList();

            logger.LogInformation("Successfully retrieved {Count} guards", guardDtos.Count);

            return new GetAllGuardsResult(guardDtos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting guards from cache database");
            throw;
        }
    }
}
