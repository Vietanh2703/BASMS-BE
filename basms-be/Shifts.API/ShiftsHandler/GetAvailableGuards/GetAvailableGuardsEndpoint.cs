namespace Shifts.API.Handlers.GetAvailableGuards;

public class GetAvailableGuardsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /shifts/available-guards
        app.MapGet("/api/shifts/available-guards", async (
            Guid locationId,
            DateTime shiftDate,
            TimeSpan startTime,
            TimeSpan endTime,
            ISender sender) =>
        {
            var query = new GetAvailableGuardsQuery(
                locationId,
                shiftDate,
                startTime,
                endTime
            );

            var result = await sender.Send(query);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Shifts")
        .WithName("GetAvailableGuards")
        .Produces<GetAvailableGuardsResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get available guards for a shift time slot")
        .WithDescription(@"Returns a list of all guards with their availability status:
            - Available: Guard is free and can be assigned
            - Busy: Guard already has a conflicting shift
            - OnLeave: Guard has approved leave request

            This helps managers easily see which guards can be assigned to a shift.");
    }
}
