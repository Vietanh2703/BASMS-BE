namespace Contracts.API.ContractsHandler.CheckLocationClosed;

public class CheckLocationClosedEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/locations/{locationId}/check-closed?date=2025-01-01
        app.MapGet("/api/locations/{locationId:guid}/check-closed",
            async (Guid locationId, DateTime date, ISender sender) =>
            {
                var query = new CheckLocationClosedQuery(locationId, date);
                var result = await sender.Send(query);
                return Results.Ok(result);
            })
        .WithTags("Locations")
        .WithName("CheckLocationClosed")
        .Produces<CheckLocationClosedResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Check if location is closed")
        .WithDescription("Returns if location is closed on specific date (special days)");
    }
}
