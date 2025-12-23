namespace Contracts.API.ContractsHandler.GetContractIdByCustomer;

public class GetContractIdByCustomerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/customers/{customerId}/contracts", async (
            Guid customerId,
            ISender sender,
            ILogger<GetContractIdByCustomerEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get contract IDs request for customer: {CustomerId}", customerId);

                var query = new GetContractIdByCustomerQuery(customerId);
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogWarning("Failed to get contracts: {ErrorMessage}", result.ErrorMessage);
                    return Results.NotFound(new
                    {
                        success = false,
                        error = result.ErrorMessage
                    });
                }

                logger.LogInformation(
                    "Successfully retrieved {Count} contract(s) for customer {CustomerCode}",
                    result.Contracts.Count, result.CustomerCode);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get contracts request for customer: {CustomerId}", customerId);
                return Results.Problem(
                    title: "Error getting contracts",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Customers")
        .WithName("GetContractIdByCustomer")
        .Produces<GetContractIdByCustomerResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy danh sách contract IDs theo customer ID");
    }
}
