namespace Contracts.API.ContractsHandler.DeleteShiftSchedules;

// ================================================================
// COMMAND & RESULT
// ================================================================

/// <summary>
/// Command để xóa shift schedule
/// </summary>
public record DeleteShiftSchedulesCommand(Guid ShiftScheduleId) : ICommand<DeleteShiftSchedulesResult>;

/// <summary>
/// Kết quả xóa shift schedule
/// </summary>
public record DeleteShiftSchedulesResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? ShiftScheduleId { get; init; }
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// DTO cho query result
/// </summary>
internal record ShiftScheduleInfo
{
    public Guid Id { get; init; }
    public string ScheduleName { get; init; } = string.Empty;
}

/// <summary>
/// Handler để xóa shift schedule (hard delete)
/// </summary>
internal class DeleteShiftSchedulesHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<DeleteShiftSchedulesHandler> logger)
    : ICommandHandler<DeleteShiftSchedulesCommand, DeleteShiftSchedulesResult>
{
    public async Task<DeleteShiftSchedulesResult> Handle(
        DeleteShiftSchedulesCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Deleting shift schedule: {ShiftScheduleId}", request.ShiftScheduleId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // 1. CHECK IF SHIFT SCHEDULE EXISTS
            // ================================================================
            var existingSchedule = await connection.QuerySingleOrDefaultAsync<ShiftScheduleInfo>(
                "SELECT Id, ScheduleName FROM contract_shift_schedules WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.ShiftScheduleId });

            if (existingSchedule == null)
            {
                logger.LogWarning("Shift schedule not found: {ShiftScheduleId}", request.ShiftScheduleId);
                return new DeleteShiftSchedulesResult
                {
                    Success = false,
                    ErrorMessage = $"Shift schedule with ID {request.ShiftScheduleId} not found"
                };
            }

            // ================================================================
            // 2. DELETE SHIFT SCHEDULE (HARD DELETE)
            // ================================================================
            var deleteQuery = @"
                DELETE FROM contract_shift_schedules
                WHERE Id = @ShiftScheduleId
            ";

            var rowsAffected = await connection.ExecuteAsync(deleteQuery, new
            {
                ShiftScheduleId = request.ShiftScheduleId
            });

            if (rowsAffected == 0)
            {
                logger.LogWarning("No rows affected when deleting shift schedule: {ShiftScheduleId}", request.ShiftScheduleId);
                return new DeleteShiftSchedulesResult
                {
                    Success = false,
                    ErrorMessage = "Failed to delete shift schedule"
                };
            }

            logger.LogInformation("Successfully deleted shift schedule {ScheduleName} (ID: {ShiftScheduleId})",
                existingSchedule.ScheduleName, request.ShiftScheduleId);

            return new DeleteShiftSchedulesResult
            {
                Success = true,
                ShiftScheduleId = request.ShiftScheduleId
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting shift schedule: {ShiftScheduleId}", request.ShiftScheduleId);
            return new DeleteShiftSchedulesResult
            {
                Success = false,
                ErrorMessage = $"Error deleting shift schedule: {ex.Message}"
            };
        }
    }
}
