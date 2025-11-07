namespace Contracts.API.ContractsHandler.ValidateLocation;

public class ValidateLocationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/{contractId}/locations/{locationId}/validate
        app.MapGet("/api/contracts/{contractId:guid}/locations/{locationId:guid}/validate",
            async (Guid contractId, Guid locationId, ISender sender) =>
            {
                var query = new ValidateLocationQuery(contractId, locationId);
                var result = await sender.Send(query);
                return Results.Ok(result);
            })
        .WithTags("Contracts")
        .WithName("ValidateLocation")
        .Produces<ValidateLocationResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Validate location in contract")
        .WithDescription("Check if location belongs to contract and is active");
    }
}
