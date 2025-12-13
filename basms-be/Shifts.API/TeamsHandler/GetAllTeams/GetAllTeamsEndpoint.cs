namespace Shifts.API.TeamsHandler.GetAllTeams;

/// <summary>
/// Endpoint để lấy danh sách teams với filtering
/// </summary>
public class GetAllTeamsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/teams", async (
    [FromQuery] Guid? managerId,
    [FromQuery] string? specialization,
    [FromQuery] bool? isActive,
    ISender sender,
    ILogger<GetAllTeamsEndpoint> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation(
        "GET /api/shifts/teams - Getting all teams");

    var query = new GetAllTeamsQuery(
        ManagerId: managerId,
        Specialization: specialization,
        IsActive: isActive
    );

    var result = await sender.Send(query, cancellationToken);

    logger.LogInformation(
        "✓ Retrieved {Count} teams",
        result.Teams.Count);

    return Results.Ok(result);
})
        // .RequireAuthorization()
        .WithName("GetAllTeams")
        .WithTags("Teams")
        .Produces(200)
        .Produces(400)
        .WithSummary("Get all teams with filtering")
        .WithDescription(@"
            Returns all teams with optional filtering.

            Query Parameters:
            - managerId (optional): Filter teams by manager ID
            - specialization (optional): Filter by specialization (RESIDENTIAL, COMMERCIAL, EVENT, VIP, INDUSTRIAL)
            - isActive (optional): Filter by active status (true/false)

            Response includes:
            - List of teams with manager info
            - Total count

            Examples:
            GET /api/shifts/teams
            GET /api/shifts/teams?managerId={guid}
            GET /api/shifts/teams?specialization=RESIDENTIAL&isActive=true
        ");
    }
}
