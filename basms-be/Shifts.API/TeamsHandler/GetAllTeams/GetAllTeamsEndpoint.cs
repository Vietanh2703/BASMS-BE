namespace Shifts.API.TeamsHandler.GetAllTeams;

/// <summary>
/// Endpoint để lấy danh sách teams theo manager
/// </summary>
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
    logger.LogInformation(
        "GET /api/shifts/teams - Getting teams for manager {ManagerId}",
        managerId);

    var query = new GetAllTeamsQuery(ManagerId: managerId);

    var result = await sender.Send(query, cancellationToken);

    logger.LogInformation(
        "✓ Retrieved {Count} teams for manager {ManagerId}",
        result.Teams.Count,
        managerId);

    return Results.Ok(result);
})
        // .RequireAuthorization()
        .WithName("GetAllTeams")
        .WithTags("Teams")
        .Produces(200)
        .Produces(400)
        .WithSummary("Get all teams by manager")
        .WithDescription(@"
            Returns all teams for a specific manager.

            Query Parameters:
            - managerId (required): Manager ID to filter teams

            Response includes:
            - List of teams with manager info
            - Total count

            Example:
            GET /api/shifts/teams?managerId={guid}
        ");
    }
}
