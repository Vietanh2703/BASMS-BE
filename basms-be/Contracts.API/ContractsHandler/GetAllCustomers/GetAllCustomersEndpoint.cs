namespace Contracts.API.ContractsHandler.GetAllCustomers;

public class GetAllCustomersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {

        app.MapGet("/api/contracts/customers", async (
            ISender sender,
            ILogger<GetAllCustomersEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get all customers request");

                var query = new GetAllCustomersQuery();
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogError("Failed to get customers: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error getting customers",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }

                logger.LogInformation(
                    "Successfully retrieved {Count} customers",
                    result.TotalCount);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get all customers request");
                return Results.Problem(
                    title: "Error getting customers",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Customers")
        .WithName("GetAllCustomers")
        .Produces<GetAllCustomersResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy tất cả customers");
    }
}
