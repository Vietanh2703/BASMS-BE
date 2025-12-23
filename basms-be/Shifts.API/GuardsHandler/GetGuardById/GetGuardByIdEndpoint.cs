namespace Shifts.API.GuardsHandler.GetGuardById;

public class GetGuardByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/guards/{id:guid}", async (Guid id, ISender sender) =>
        {
            var query = new GetGuardByIdQuery(id);
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Guards")
        .WithName("GetGuardById")
        .Produces<GetGuardByIdResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get guard by ID");
    }
}