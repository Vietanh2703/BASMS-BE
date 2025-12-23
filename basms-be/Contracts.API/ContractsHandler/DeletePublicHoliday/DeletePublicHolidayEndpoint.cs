namespace Contracts.API.ContractsHandler.DeletePublicHoliday;

public class DeletePublicHolidayEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/contracts/holidays/{holidayId}", async (
            Guid holidayId,
            ISender sender,
            ILogger<DeletePublicHolidayEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Delete public holiday request for ID: {HolidayId}", holidayId);

                var command = new DeletePublicHolidayCommand(holidayId);
                var result = await sender.Send(command);

                if (!result.Success)
                {
                    logger.LogError("Failed to delete holiday: {ErrorMessage}", result.ErrorMessage);
                    return Results.NotFound(new
                    {
                        success = false,
                        error = result.ErrorMessage
                    });
                }

                logger.LogInformation(
                    "Successfully deleted holiday {HolidayName} (ID: {HolidayId})",
                    result.HolidayName, result.HolidayId);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing delete holiday request for ID: {HolidayId}", holidayId);
                return Results.Problem(
                    title: "Error deleting holiday",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Holidays")
        .WithName("DeletePublicHoliday")
        .Produces<DeletePublicHolidayResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Xóa public holiday");
    }
}
