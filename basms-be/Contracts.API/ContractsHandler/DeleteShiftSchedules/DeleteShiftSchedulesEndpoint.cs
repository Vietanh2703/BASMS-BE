namespace Contracts.API.ContractsHandler.DeleteShiftSchedules;

/// <summary>
/// Endpoint để xóa shift schedule
/// </summary>
public class DeleteShiftSchedulesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: DELETE /api/contracts/shift-schedules/{shiftScheduleId}
        app.MapDelete("/api/contracts/shift-schedules/{shiftScheduleId}", async (
            Guid shiftScheduleId,
            ISender sender,
            ILogger<DeleteShiftSchedulesEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Delete shift schedule request for ID: {ShiftScheduleId}", shiftScheduleId);

                var command = new DeleteShiftSchedulesCommand(shiftScheduleId);
                var result = await sender.Send(command);

                if (!result.Success)
                {
                    logger.LogError("Failed to delete shift schedule: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error deleting shift schedule",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                logger.LogInformation("Successfully deleted shift schedule: {ShiftScheduleId}", result.ShiftScheduleId);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing delete shift schedule request for ID: {ShiftScheduleId}", shiftScheduleId);
                return Results.Problem(
                    title: "Error deleting shift schedule",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Shift Schedules")
        .WithName("DeleteShiftSchedules")
        .Produces<DeleteShiftSchedulesResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Xóa shift schedule");
    }
}
