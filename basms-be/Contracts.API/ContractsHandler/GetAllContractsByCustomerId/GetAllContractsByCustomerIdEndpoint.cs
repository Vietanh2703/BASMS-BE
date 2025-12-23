namespace Contracts.API.ContractsHandler.GetAllContractsByCustomerId;

public class GetAllContractsByCustomerIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/customers/{customerId}/all", async (
            Guid customerId,
            ISender sender,
            ILogger<GetAllContractsByCustomerIdEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get all contracts (full details) request for customer: {CustomerId}", customerId);

                var query = new GetAllContractsByCustomerIdQuery(customerId);
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
                    "Successfully retrieved {Count} contract(s) with full details for customer {CustomerCode} ({CustomerName})",
                    result.TotalContracts, result.CustomerCode, result.CustomerName);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get all contracts request for customer: {CustomerId}", customerId);
                return Results.Problem(
                    title: "Error getting contracts",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Customers")
        .WithName("GetAllContractsByCustomerId")
        .Produces<GetAllContractsByCustomerIdResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy tất cả contracts (full details) theo customer ID");
    }
}
