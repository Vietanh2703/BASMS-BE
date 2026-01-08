using Shifts.API.Utilities;

namespace Shifts.API.TeamsHandler.GetAllTeams;

public class GetAllTeamsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/teams", async (
            [FromQuery] Guid managerId,
            ISender sender,
            ILogger<GetAllTeamsEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("GET /api/shifts/teams - Getting teams for manager {ManagerId}", managerId);

            var query = new GetAllTeamsQuery(ManagerId: managerId);
            var result = await sender.Send(query, cancellationToken);

            logger.LogInformation("Retrieved {Count} teams for manager {ManagerId}", result.Teams.Count, managerId);

            return Results.Ok(result);
        })
        .AddStandardGetDocumentation<object>(
            tag: "Teams",
            name: "GetAllTeams",
            summary: "Get all teams by manager",
            canReturnNotFound: false);
    }
}
