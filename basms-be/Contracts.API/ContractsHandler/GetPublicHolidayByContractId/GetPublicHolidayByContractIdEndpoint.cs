namespace Contracts.API.ContractsHandler.GetPublicHolidayByContractId;

public class GetPublicHolidayByContractIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/{contractId}/public-holidays", async (
            Guid contractId,
            ISender sender,
            ILogger<GetPublicHolidayByContractIdEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get public holidays request for contract: {ContractId}", contractId);

                var query = new GetPublicHolidayByContractIdQuery(contractId);
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogWarning("Failed to get public holidays: {ErrorMessage}", result.ErrorMessage);
                    return Results.NotFound(new
                    {
                        success = false,
                        error = result.ErrorMessage
                    });
                }

                logger.LogInformation(
                    "Successfully retrieved {Count} public holiday(s) for contract {ContractCode}",
                    result.PublicHolidays.Count, result.ContractCode);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get public holidays request for contract: {ContractId}", contractId);
                return Results.Problem(
                    title: "Error getting public holidays",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Public Holidays")
        .WithName("GetPublicHolidayByContractId")
        .Produces<GetPublicHolidayByContractIdResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy danh sách public holidays theo contract ID");
    }
}
