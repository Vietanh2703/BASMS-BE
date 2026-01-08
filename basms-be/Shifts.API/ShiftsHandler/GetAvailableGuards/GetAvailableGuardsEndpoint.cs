namespace Shifts.API.ShiftsHandler.GetAvailableGuards;

public class GetAvailableGuardsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
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
        .AddStandardGetDocumentation<GetAvailableGuardsResult>(
            tag: "Shifts",
            name: "GetAvailableGuards",
            summary: "Get available guards for a shift time slot",
            description: @"Returns a list of all guards with their availability status:
            - Available: Guard is free and can be assigned
            - Busy: Guard already has a conflicting shift
            - OnLeave: Guard has approved leave request

            This helps managers easily see which guards can be assigned to a shift.",
            canReturnNotFound: false);
    }
}
