namespace Incidents.API.IncidentHandler.GetAllIncidents;

/// <summary>
/// Endpoint để lấy danh sách tất cả incidents
/// </summary>
public class GetAllIncidentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/incidents/get-all", async (
    ISender sender,
    ILogger<GetAllIncidentsEndpoint> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation(
        "GET /api/incidents/get-all - Getting all incidents");

    var query = new GetAllIncidentsQuery();

    var result = await sender.Send(query, cancellationToken);

    if (!result.Success)
    {
        logger.LogWarning(
            "Failed to get incidents: {Error}",
            result.ErrorMessage);

        return Results.BadRequest(new
        {
            success = false,
            error = result.ErrorMessage
        });
    }

    logger.LogInformation(
        "✓ Retrieved {Count} incidents",
        result.Incidents.Count);

    return Results.Ok(new
    {
        success = true,
        data = result.Incidents,
        totalCount = result.TotalCount,
        message = "Incidents sorted by incident time (newest first)"
    });
})
        // .RequireAuthorization()
        .WithName("GetAllIncidents")
        .WithTags("Incidents")
        .Produces(200)
        .Produces(400)
        .WithSummary("Get all incidents")
        .WithDescription(@"
            Returns all incidents sorted by incident time (newest first).

            Incident Types:
            - INTRUSION: Security breach/intrusion
            - THEFT: Theft incident
            - FIRE: Fire incident
            - MEDICAL: Medical emergency
            - EQUIPMENT_FAILURE: Equipment malfunction
            - VANDALISM: Vandalism
            - DISPUTE: Dispute/conflict
            - OTHER: Other types

            Severity Levels:
            - LOW: Low severity
            - MEDIUM: Medium severity
            - HIGH: High severity
            - CRITICAL: Critical severity

            Status:
            - REPORTED: Newly reported
            - IN_PROGRESS: Being handled
            - RESOLVED: Resolved
            - ESCALATED: Escalated to higher authority
            - CLOSED: Closed

            Examples:
            GET /api/incidents/get-all
        ");
    }
}
