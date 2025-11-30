// Handler xử lý logic lấy thông tin guard theo ID
// Query từ cache database
namespace Shifts.API.GuardsHandler.GetGuardById;

// Query chứa ID guard cần lấy
public record GetGuardByIdQuery(Guid Id) : IQuery<GetGuardByIdResult>;

// Result chứa guard detail DTO
public record GetGuardByIdResult(GuardDetailDto Guard);

// DTO chứa đầy đủ thông tin guard
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

            // Bước 1: Tạo kết nối database
            using var connection = await connectionFactory.CreateConnectionAsync();

            // Bước 2: Lấy guard theo ID từ cache
            var guards = await connection.GetAllAsync<Guards>();
            var guard = guards.FirstOrDefault(g => g.Id == request.Id && !g.IsDeleted);

            if (guard == null)
            {
                logger.LogWarning("Guard not found with ID: {GuardId}", request.Id);
                throw new InvalidOperationException($"Guard with ID {request.Id} not found");
            }

            // Bước 3: Map entity sang DTO
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

            // Bước 4: Trả về kết quả
            return new GetGuardByIdResult(guardDetailDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting guard by ID: {GuardId}", request.Id);
            throw;
        }
    }
}
