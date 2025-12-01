namespace Contracts.API.ContractsHandler.DeletePublicHoliday;

// ================================================================
// COMMAND & RESULT
// ================================================================

/// <summary>
/// Command để xóa public holiday
/// </summary>
public record DeletePublicHolidayCommand(Guid HolidayId) : ICommand<DeletePublicHolidayResult>;

/// <summary>
/// Kết quả delete holiday
/// </summary>
public record DeletePublicHolidayResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? HolidayId { get; init; }
    public string? HolidayName { get; init; }
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// Handler để xóa public holiday
/// </summary>
internal class DeletePublicHolidayHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<DeletePublicHolidayHandler> logger)
    : ICommandHandler<DeletePublicHolidayCommand, DeletePublicHolidayResult>
{
    public async Task<DeletePublicHolidayResult> Handle(
        DeletePublicHolidayCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Deleting public holiday: {HolidayId}", request.HolidayId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // 1. CHECK IF HOLIDAY EXISTS
            // ================================================================
            var checkQuery = @"
                SELECT HolidayName
                FROM public_holidays
                WHERE Id = @HolidayId
            ";

            var holidayName = await connection.QuerySingleOrDefaultAsync<string>(
                checkQuery,
                new { HolidayId = request.HolidayId });

            if (holidayName == null)
            {
                logger.LogWarning("Public holiday not found: {HolidayId}", request.HolidayId);
                return new DeletePublicHolidayResult
                {
                    Success = false,
                    ErrorMessage = $"Public holiday with ID {request.HolidayId} not found"
                };
            }

            // ================================================================
            // 2. DELETE PUBLIC HOLIDAY (Hard delete)
            // ================================================================
            var deleteQuery = @"
                DELETE FROM public_holidays
                WHERE Id = @HolidayId
            ";

            var rowsAffected = await connection.ExecuteAsync(
                deleteQuery,
                new { HolidayId = request.HolidayId });

            if (rowsAffected == 0)
            {
                logger.LogWarning("No rows affected when deleting holiday: {HolidayId}", request.HolidayId);
                return new DeletePublicHolidayResult
                {
                    Success = false,
                    ErrorMessage = "Failed to delete holiday"
                };
            }

            logger.LogInformation(
                "Successfully deleted holiday {HolidayName} (ID: {HolidayId})",
                holidayName, request.HolidayId);

            return new DeletePublicHolidayResult
            {
                Success = true,
                HolidayId = request.HolidayId,
                HolidayName = holidayName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting public holiday: {HolidayId}", request.HolidayId);
            return new DeletePublicHolidayResult
            {
                Success = false,
                ErrorMessage = $"Error deleting holiday: {ex.Message}"
            };
        }
    }
}
