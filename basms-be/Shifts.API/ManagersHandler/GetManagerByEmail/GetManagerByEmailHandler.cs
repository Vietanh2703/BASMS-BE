// Handler xử lý logic lấy thông tin manager theo Email
// Query từ cache database
using Shifts.API.ManagersHandler.GetManagerById;

namespace Shifts.API.ManagersHandler.GetManagerByEmail;

// Query chứa Email manager cần lấy
public record GetManagerByEmailQuery(string Email) : IQuery<GetManagerByEmailResult>;

// Result chứa manager detail DTO
public record GetManagerByEmailResult(ManagerDetailDto Manager);

internal class GetManagerByEmailHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetManagerByEmailHandler> logger)
    : IQueryHandler<GetManagerByEmailQuery, GetManagerByEmailResult>
{
    public async Task<GetManagerByEmailResult> Handle(GetManagerByEmailQuery request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting manager by Email: {Email}", request.Email);

            // Bước 1: Tạo kết nối database
            using var connection = await connectionFactory.CreateConnectionAsync();

            // Bước 2: Lấy manager theo Email từ cache (sử dụng index trên Email column)
            var managers = await connection.GetAllAsync<Managers>();
            var manager = managers.FirstOrDefault(m =>
                m.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase) &&
                !m.IsDeleted);

            if (manager == null)
            {
                logger.LogWarning("Manager not found with Email: {Email}", request.Email);
                throw new InvalidOperationException($"Manager with Email {request.Email} not found");
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

            logger.LogInformation("Successfully retrieved manager: {EmployeeCode} with email {Email}",
                manager.EmployeeCode, manager.Email);

            // Bước 4: Trả về kết quả
            return new GetManagerByEmailResult(managerDetailDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting manager by Email: {Email}", request.Email);
            throw;
        }
    }
}
