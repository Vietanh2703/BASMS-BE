namespace Contracts.API.ContractsHandler.ValidateContract;

public class ValidateContractEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/{id}/validate
        app.MapGet("/api/contracts/{id:guid}/validate", async (Guid id, ISender sender) =>
        {
            var query = new ValidateContractQuery(id);
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithTags("Contracts")
        .WithName("ValidateContract")
        .Produces<ValidateContractResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Validate contract")
        .WithDescription("Check if contract exists and is active");
    }
}
