namespace Shifts.API.GuardsHandler.GetGuardById;

public record GetGuardByIdQuery(Guid Id) : IQuery<GetGuardByIdResult>;

public record GetGuardByIdResult(GuardDetailDto Guard);

public record GuardDetailDto(
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
    string? CertificationLevel,
    DateTime? TerminationDate,
    string? TerminationReason,
    int MaxWeeklyHours,
    bool CanWorkOvertime,
    bool CanWorkWeekends,
    bool CanWorkHolidays,
    string CurrentAvailability,
    bool IsActive,
    DateTime? LastSyncedAt,
    string SyncStatus,
    int? UserServiceVersion,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

internal class GetGuardByIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetGuardByIdHandler> logger)
    : IQueryHandler<GetGuardByIdQuery, GetGuardByIdResult>
{
    public async Task<GetGuardByIdResult> Handle(GetGuardByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting guard by ID: {GuardId}", request.Id);

            // Sử dụng ExecuteQueryAsync helper để clean code
            var guard = await connectionFactory.ExecuteQueryAsync(
                async connection => await connection.GetGuardByIdOrThrowAsync(request.Id),
                logger,
                "GetGuardById");

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

            logger.LogInformation("Successfully retrieved guard: {EmployeeCode}", guard.EmployeeCode);
            
            return new GetGuardByIdResult(guardDetailDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting guard by ID: {GuardId}", request.Id);
            throw;
        }
    }
}
