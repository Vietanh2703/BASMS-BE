namespace Contracts.API.ContractsHandler.GetShiftScheduleByContractId;

public class GetShiftScheduleByContractIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/{contractId}/shift-schedules", async (
            Guid contractId,
            ISender sender,
            ILogger<GetShiftScheduleByContractIdEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get shift schedules request for contract: {ContractId}", contractId);

                var query = new GetShiftScheduleByContractIdQuery(contractId);
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogWarning("Failed to get shift schedules: {ErrorMessage}", result.ErrorMessage);
                    return Results.NotFound(new
                    {
                        success = false,
                        error = result.ErrorMessage
                    });
                }

                logger.LogInformation(
                    "Successfully retrieved {Count} shift schedule(s) for contract {ContractCode}",
                    result.ShiftSchedules.Count, result.ContractCode);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get shift schedules request for contract: {ContractId}", contractId);
                return Results.Problem(
                    title: "Error getting shift schedules",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Shift Schedules")
        .WithName("GetShiftScheduleByContractId")
        .Produces<GetShiftScheduleByContractIdResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy danh sách shift schedules theo contract ID");
    }
}
