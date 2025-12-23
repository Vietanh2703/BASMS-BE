namespace Contracts.API.ContractsHandler.GetCustomerById;

public class GetCustomerByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/customers/{id:guid}", async (
            Guid id,
            ISender sender,
            ILogger<GetCustomerByIdEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get customer detail request for ID: {CustomerId}", id);

                var query = new GetCustomerByIdQuery(id);
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogError("Failed to get customer detail: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error getting customer detail",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status404NotFound
                    );
                }

                logger.LogInformation(
                    "Successfully retrieved customer {CustomerCode}",
                    result.Customer?.CustomerCode);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get customer detail request for ID: {CustomerId}", id);
                return Results.Problem(
                    title: "Error getting customer detail",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Customers")
        .WithName("GetCustomerById")
        .Produces<GetCustomerByIdResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy thông tin chi tiết customer theo ID");
    }
}
