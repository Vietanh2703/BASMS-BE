// Handler xử lý logic lấy tất cả guards đã joined theo ManagerId
// Query từ cache database với filter ContractType = "joined_in"
using Shifts.API.GuardsHandler.GetGuardById;

namespace Shifts.API.GuardsHandler.GetAllGuardJoined;

// Query chứa ManagerId để lấy tất cả guards đã joined
public record GetAllGuardJoinedQuery(Guid ManagerId) : IQuery<GetAllGuardJoinedResult>;

// Result chứa danh sách guards đã joined
public record GetAllGuardJoinedResult(
    Guid ManagerId,
    int TotalGuards,
    List<GuardDetailDto> Guards
);

internal class GetAllGuardJoinedHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetAllGuardJoinedHandler> logger)
    : IQueryHandler<GetAllGuardJoinedQuery, GetAllGuardJoinedResult>
{
    public async Task<GetAllGuardJoinedResult> Handle(
        GetAllGuardJoinedQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting all joined guards for Manager: {ManagerId}",
                request.ManagerId);

            // Bước 1: Tạo kết nối database
            using var connection = await connectionFactory.CreateConnectionAsync();

            // Bước 2: Lấy tất cả guards có DirectManagerId trùng với ManagerId
            // và ContractType = "joined_in"
            var allGuards = await connection.GetAllAsync<Guards>();
            var guards = allGuards
                .Where(g =>
                    g.DirectManagerId == request.ManagerId &&
                    g.ContractType == "joined_in" &&
                    !g.IsDeleted)
                .OrderBy(g => g.EmployeeCode)
                .ToList();

            logger.LogInformation(
                "Found {Count} joined guards for Manager {ManagerId}",
                guards.Count,
                request.ManagerId);

            // Bước 3: Map entities sang DTOs
            var guardDtos = guards.Select(guard => new GuardDetailDto(
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
                "Successfully retrieved {Count} joined guards for Manager {ManagerId}",
                guardDtos.Count,
                request.ManagerId);

            // Bước 4: Trả về kết quả
            return new GetAllGuardJoinedResult(
                ManagerId: request.ManagerId,
                TotalGuards: guardDtos.Count,
                Guards: guardDtos
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting joined guards for Manager: {ManagerId}", request.ManagerId);
            throw;
        }
    }
}
