// Handler xử lý logic lấy thông tin manager theo ID
// Query từ cache database
namespace Shifts.API.ManagersHandler.GetManagerById;

// Query chứa ID manager cần lấy
public record GetManagerByIdQuery(Guid Id) : IQuery<GetManagerByIdResult>;

// Result chứa manager detail DTO
public record GetManagerByIdResult(ManagerDetailDto Manager);

// DTO chứa đầy đủ thông tin manager
public record ManagerDetailDto(
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
    int? MaxTeamSize,
    int TotalTeamsManaged,
    int TotalGuardsSupervised,
    int TotalShiftsCreated,
    bool IsActive,
    DateTime? LastSyncedAt,
    string SyncStatus,
    int? UserServiceVersion,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

internal class GetManagerByIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetManagerByIdHandler> logger)
    : IQueryHandler<GetManagerByIdQuery, GetManagerByIdResult>
{
    public async Task<GetManagerByIdResult> Handle(GetManagerByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting manager by ID: {ManagerId}", request.Id);

            // Bước 1: Tạo kết nối database
            using var connection = await connectionFactory.CreateConnectionAsync();

            // Bước 2: Lấy manager theo ID từ cache
            var managers = await connection.GetAllAsync<Managers>();
            var manager = managers.FirstOrDefault(m => m.Id == request.Id && !m.IsDeleted);

            if (manager == null)
            {
                logger.LogWarning("Manager not found with ID: {ManagerId}", request.Id);
                throw new InvalidOperationException($"Manager with ID {request.Id} not found");
            }

            // Bước 3: Map entity sang DTO
            var managerDetailDto = new ManagerDetailDto(
                Id: manager.Id,
                IdentityNumber: manager.IdentityNumber,
                EmployeeCode: manager.EmployeeCode,
                FullName: manager.FullName,
                Email: manager.Email,
                AvatarUrl: manager.AvatarUrl,
                PhoneNumber: manager.PhoneNumber,
                CurrentAddress: manager.CurrentAddress,
                Gender: manager.Gender,
                DateOfBirth: manager.DateOfBirth,
                Role: manager.Role,
                Position: manager.Position,
                Department: manager.Department,
                ManagerLevel: manager.ManagerLevel,
                ReportsToManagerId: manager.ReportsToManagerId,
                EmploymentStatus: manager.EmploymentStatus,
                CanCreateShifts: manager.CanCreateShifts,
                CanApproveShifts: manager.CanApproveShifts,
                CanAssignGuards: manager.CanAssignGuards,
                CanApproveOvertime: manager.CanApproveOvertime,
                CanManageTeams: manager.CanManageTeams,
                MaxTeamSize: manager.MaxTeamSize,
                TotalTeamsManaged: manager.TotalTeamsManaged,
                TotalGuardsSupervised: manager.TotalGuardsSupervised,
                TotalShiftsCreated: manager.TotalShiftsCreated,
                IsActive: manager.IsActive,
                LastSyncedAt: manager.LastSyncedAt,
                SyncStatus: manager.SyncStatus,
                UserServiceVersion: manager.UserServiceVersion,
                CreatedAt: manager.CreatedAt,
                UpdatedAt: manager.UpdatedAt
            );

            logger.LogInformation("Successfully retrieved manager: {EmployeeCode}", manager.EmployeeCode);

            // Bước 4: Trả về kết quả
            return new GetManagerByIdResult(managerDetailDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting manager by ID: {ManagerId}", request.Id);
            throw;
        }
    }
}
