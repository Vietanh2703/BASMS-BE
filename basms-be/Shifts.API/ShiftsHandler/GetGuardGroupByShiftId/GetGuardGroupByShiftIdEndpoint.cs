namespace Shifts.API.ShiftsHandler.GetGuardGroupByShiftId;

public class GetGuardGroupByShiftIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/{shiftId:guid}/guards", async (
            [FromRoute] Guid shiftId,
            ISender sender,
            ILogger<GetGuardGroupByShiftIdEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/shifts/{ShiftId}/guards - Getting guard group for shift",
                shiftId);

            var query = new GetGuardGroupByShiftIdQuery(shiftId);

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to get guard group for Shift {ShiftId}: {Error}",
                    shiftId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "Found {Count} guards for Shift {ShiftId}",
                result.TotalGuards,
                shiftId);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    shiftId = result.ShiftId,
                    teamId = result.TeamId,
                    teamName = result.TeamName,
                    guards = result.Guards,
                    totalGuards = result.TotalGuards
                }
            });
        })
        .RequireAuthorization()
        .WithName("GetGuardGroupByShiftId")
        .WithTags("Shifts - Guards")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithSummary("Lấy danh sách guards được phân công vào shift");
    }
}
