namespace Shifts.API.GuardsHandler.GetAllGuards;

public class GetAllGuardsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/guards", async (ISender sender) =>
        {
            var query = new GetAllGuardsQuery();
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Guards")
        .WithName("GetAllGuards")
        .Produces<GetAllGuardsResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get all guards")
        .WithDescription("Retrieves all active guards from the cache database");
    }
}
