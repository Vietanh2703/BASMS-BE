namespace Incidents.API.IncidentHandler.GetAllIncidents;

/// <summary>
/// Endpoint để lấy danh sách tất cả incidents với filtering
/// </summary>
public class GetAllIncidentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/incidents/get-all", async (
    [FromQuery] Guid? reporterId,
    [FromQuery] Guid? responderId,
    [FromQuery] Guid? shiftId,
    [FromQuery] Guid? locationId,
    [FromQuery] DateTime? fromDate,
    [FromQuery] DateTime? toDate,
    [FromQuery] string? status,
    [FromQuery] string? incidentType,
    [FromQuery] string? severity,
    ISender sender,
    ILogger<GetAllIncidentsEndpoint> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation(
        "GET /api/incidents/get-all - Getting all incidents");

    var query = new GetAllIncidentsQuery(
        ReporterId: reporterId,
        ResponderId: responderId,
        ShiftId: shiftId,
        LocationId: locationId,
        FromDate: fromDate,
        ToDate: toDate,
        Status: status,
        IncidentType: incidentType,
        Severity: severity
    );

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
        message = "Incidents sorted by incident time (newest first)",
        filters = new
        {
            reporterId = reporterId?.ToString() ?? "all",
            responderId = responderId?.ToString() ?? "all",
            shiftId = shiftId?.ToString() ?? "all",
            locationId = locationId?.ToString() ?? "all",
            fromDate = fromDate?.ToString("yyyy-MM-dd") ?? "all",
            toDate = toDate?.ToString("yyyy-MM-dd") ?? "all",
            status = status ?? "all",
            incidentType = incidentType ?? "all",
            severity = severity ?? "all"
        }
    });
})
        // .RequireAuthorization()
        .WithName("GetAllIncidents")
        .WithTags("Incidents")
        .Produces(200)
        .Produces(400)
        .WithSummary("Get all incidents with filtering")
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

            Query Parameters:
            - reporterId (optional): Filter by reporter (user who reported the incident)
            - responderId (optional): Filter by responder (user handling the incident)
            - shiftId (optional): Filter incidents related to a specific shift
            - locationId (optional): Filter by location
            - fromDate (optional): Filter incidents from this date (yyyy-MM-dd)
            - toDate (optional): Filter incidents until this date (yyyy-MM-dd)
            - status (optional): Filter by status (REPORTED, IN_PROGRESS, RESOLVED, ESCALATED, CLOSED)
            - incidentType (optional): Filter by incident type
            - severity (optional): Filter by severity (LOW, MEDIUM, HIGH, CRITICAL)

            Examples:
            GET /api/incidents/get-all
            GET /api/incidents/get-all?reporterId={guid}
            GET /api/incidents/get-all?fromDate=2025-01-01&toDate=2025-01-31
            GET /api/incidents/get-all?status=REPORTED
            GET /api/incidents/get-all?severity=HIGH
            GET /api/incidents/get-all?incidentType=INTRUSION
            GET /api/incidents/get-all?shiftId={guid}
            GET /api/incidents/get-all?locationId={guid}
        ");
    }
}
