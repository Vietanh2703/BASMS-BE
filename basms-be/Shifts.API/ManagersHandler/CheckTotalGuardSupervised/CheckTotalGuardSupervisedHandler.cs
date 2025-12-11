using Dapper;

namespace Shifts.API.ManagersHandler.CheckTotalGuardSupervised;

/// <summary>
/// Query để kiểm tra số guards thực tế của manager so với TotalGuardsSupervised
/// </summary>
public record CheckTotalGuardSupervisedQuery(
    Guid ManagerId
) : IQuery<CheckTotalGuardSupervisedResult>;

/// <summary>
/// Result trả về thông tin so sánh guards
/// </summary>
public record CheckTotalGuardSupervisedResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid ManagerId { get; init; }
    public string? ManagerName { get; init; }
    public int ActualGuardsCount { get; init; } // Số guards thực tế đang có DirectManagerId = ManagerId
    public int TotalGuardsSupervised { get; init; } // Số từ managers.TotalGuardsSupervised
    public int AvailableSlots { get; init; } // Số slot còn lại = TotalGuardsSupervised - ActualGuardsCount
    public bool IsOverLimit { get; init; } // Có vượt quá giới hạn không (ActualGuardsCount > TotalGuardsSupervised)
    public string Message { get; init; } = string.Empty;
};

internal class CheckTotalGuardSupervisedHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<CheckTotalGuardSupervisedHandler> logger)
    : IQueryHandler<CheckTotalGuardSupervisedQuery, CheckTotalGuardSupervisedResult>
{
    public async Task<CheckTotalGuardSupervisedResult> Handle(
        CheckTotalGuardSupervisedQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Checking guard count for Manager {ManagerId}",
                query.ManagerId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: LẤY THÔNG TIN MANAGER
            // ================================================================
            var manager = await connection.QueryFirstOrDefaultAsync<Managers>(
                @"SELECT * FROM managers
                  WHERE Id = @ManagerId
                  AND IsDeleted = 0",
                new { query.ManagerId });

            if (manager == null)
            {
                logger.LogWarning("Manager {ManagerId} not found", query.ManagerId);
                return new CheckTotalGuardSupervisedResult
                {
                    Success = false,
                    ErrorMessage = $"Manager with ID {query.ManagerId} not found",
                    ManagerId = query.ManagerId
                };
            }

            // ================================================================
            // BƯỚC 2: ĐẾM SỐ GUARDS THỰC TẾ
            // ================================================================
            var actualGuardsCount = await connection.QuerySingleAsync<int>(
                @"SELECT COUNT(*)
                  FROM guards
                  WHERE DirectManagerId = @ManagerId
                  AND IsDeleted = 0
                  AND ContractType != 'join_in_request'",
                new { query.ManagerId });

            logger.LogInformation(
                "Manager {ManagerId} has {ActualCount} guards (TotalGuardsSupervised: {Total})",
                query.ManagerId,
                actualGuardsCount,
                manager.TotalGuardsSupervised);

            // ================================================================
            // BƯỚC 3: SO SÁNH VÀ TÍNH TOÁN
            // ================================================================
            var totalGuardsSupervised = manager.TotalGuardsSupervised;
            var availableSlots = totalGuardsSupervised - actualGuardsCount;
            var isOverLimit = actualGuardsCount > totalGuardsSupervised;

            string message;
            if (isOverLimit)
            {
                message = $"⚠️ Manager has {actualGuardsCount} guards but limit is {totalGuardsSupervised}. Over limit by {actualGuardsCount - totalGuardsSupervised}.";
            }
            else if (availableSlots == 0)
            {
                message = $"Manager has reached the limit: {actualGuardsCount}/{totalGuardsSupervised} guards. No available slots.";
            }
            else
            {
                message = $"Manager has {actualGuardsCount}/{totalGuardsSupervised} guards. {availableSlots} slots available.";
            }

            return new CheckTotalGuardSupervisedResult
            {
                Success = true,
                ManagerId = query.ManagerId,
                ManagerName = manager.FullName,
                ActualGuardsCount = actualGuardsCount,
                TotalGuardsSupervised = totalGuardsSupervised,
                AvailableSlots = availableSlots,
                IsOverLimit = isOverLimit,
                Message = message
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error checking guard count for Manager {ManagerId}",
                query.ManagerId);

            return new CheckTotalGuardSupervisedResult
            {
                Success = false,
                ErrorMessage = $"Error checking guard count: {ex.Message}",
                ManagerId = query.ManagerId
            };
        }
    }
}
