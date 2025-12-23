using Shifts.API.GuardsHandler.GetGuardById;

namespace Shifts.API.GuardsHandler.GetGuardByEmail;

public record GetGuardByEmailQuery(string Email) : IQuery<GetGuardByEmailResult>;

public record GetGuardByEmailResult(GuardDetailDto Guard);

internal class GetGuardByEmailHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetGuardByEmailHandler> logger)
    : IQueryHandler<GetGuardByEmailQuery, GetGuardByEmailResult>
{
    public async Task<GetGuardByEmailResult> Handle(GetGuardByEmailQuery request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting guard by Email: {Email}", request.Email);
            using var connection = await connectionFactory.CreateConnectionAsync();
            
            var guards = await connection.GetAllAsync<Guards>();
            var guard = guards.FirstOrDefault(g =>
                !string.IsNullOrEmpty(g.Email) &&
                g.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase) &&
                !g.IsDeleted);

            if (guard == null)
            {
                logger.LogWarning("Guard not found with Email: {Email}", request.Email);
                throw new InvalidOperationException($"Guard with Email {request.Email} not found");
            }
            var guardDetailDto = new GuardDetailDto(
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
            );

            logger.LogInformation("Successfully retrieved guard: {EmployeeCode} with email {Email}",
                guard.EmployeeCode, guard.Email);
            
            return new GetGuardByEmailResult(guardDetailDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting guard by Email: {Email}", request.Email);
            throw;
        }
    }
}
