// Handler xử lý logic lấy danh sách tất cả managers
// Query từ cache database để tránh gọi Users.API
namespace Shifts.API.ManagersHandler.GetAllManagers;

public record GetAllManagersQuery() : IQuery<GetAllManagersResult>;
public record GetAllManagersResult(IEnumerable<ManagerDto> Managers);

public record ManagerDto(
    Guid Id,
    string IdentityNumber,
    string EmployeeCode,
    string FullName,
    string Email,
    string? AvatarUrl,
    string? PhoneNumber,
    string? CurrentAddress,
    string? Gender,
    DateTime? DateOfBirth,
    string Role,
    string? Position,
    string? Department,
    int ManagerLevel,
    Guid? ReportsToManagerId,
    string EmploymentStatus,
    bool CanCreateShifts,
    bool CanApproveShifts,
    bool CanAssignGuards,
    bool CanApproveOvertime,
    bool CanManageTeams,
    int TotalTeamsManaged,
    int TotalGuardsSupervised,
    int TotalShiftsCreated,
    bool IsActive,
    DateTime? LastSyncedAt,
    string SyncStatus,
    DateTime CreatedAt
);

internal class GetAllManagersHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetAllManagersHandler> logger)
    : IQueryHandler<GetAllManagersQuery, GetAllManagersResult>
{
    public async Task<GetAllManagersResult> Handle(GetAllManagersQuery request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting all managers from cache database");

            using var connection = await connectionFactory.CreateConnectionAsync();

            // Get all managers (not deleted)
            var managers = await connection.GetAllAsync<Managers>();

            // Filter out deleted managers and map to DTO
            var managerDtos = managers
                .Where(m => !m.IsDeleted && m.IsActive)
                .Select(m => new ManagerDto(
                    Id: m.Id,
                    IdentityNumber: m.IdentityNumber,
                    EmployeeCode: m.EmployeeCode,
                    FullName: m.FullName,
                    Email: m.Email,
                    AvatarUrl: m.AvatarUrl,
                    PhoneNumber: m.PhoneNumber,
                    CurrentAddress: m.CurrentAddress,
                    Gender: m.Gender,
                    DateOfBirth: m.DateOfBirth,
                    Role: m.Role,
                    Position: m.Position,
                    Department: m.Department,
                    ManagerLevel: m.ManagerLevel,
                    ReportsToManagerId: m.ReportsToManagerId,
                    EmploymentStatus: m.EmploymentStatus,
                    CanCreateShifts: m.CanCreateShifts,
                    CanApproveShifts: m.CanApproveShifts,
                    CanAssignGuards: m.CanAssignGuards,
                    CanApproveOvertime: m.CanApproveOvertime,
                    CanManageTeams: m.CanManageTeams,
                    TotalTeamsManaged: m.TotalTeamsManaged,
                    TotalGuardsSupervised: m.TotalGuardsSupervised,
                    TotalShiftsCreated: m.TotalShiftsCreated,
                    IsActive: m.IsActive,
                    LastSyncedAt: m.LastSyncedAt,
                    SyncStatus: m.SyncStatus,
                    CreatedAt: m.CreatedAt
                ))
                .OrderByDescending(m => m.CreatedAt)
                .ToList();

            logger.LogInformation("Successfully retrieved {Count} managers", managerDtos.Count);

            return new GetAllManagersResult(managerDtos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting managers from cache database");
            throw;
        }
    }
}
