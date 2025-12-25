namespace Incidents.API.IncidentHandler.GetIncidentById;

public class GetIncidentByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/incidents/{incidentId:guid}", async (
            Guid incidentId,
            ISender sender,
            ILogger<GetIncidentByIdEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/incidents/{IncidentId} - Getting incident details",
                incidentId);

            var query = new GetIncidentByIdQuery(incidentId);

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to get incident {IncidentId}: {Error}",
                    incidentId,
                    result.ErrorMessage);

                return Results.NotFound(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "✓ Retrieved incident {IncidentCode}",
                result.Incident!.IncidentCode);

            return Results.Ok(new
            {
                success = true,
                data = result.Incident
            });
        })
        .RequireAuthorization()
        .WithName("GetIncidentById")
        .WithTags("Incidents")
        .Produces(200)
        .Produces(404)
        .Produces(401)
        .WithSummary("Get incident by ID")
        .WithDescription(@"
            Returns detailed information about a specific incident including all media files.

            Route Parameters:
            - incidentId: GUID of the incident

            Response includes:
            - Incident basic information (code, title, description)
            - Classification (type, severity)
            - Time and location details
            - Reporter information
            - Response information (if responded)
            - All media files (images, videos) with metadata
            - Audit information (created/updated timestamps)

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
            - RESPONDED: Response provided
            - RESOLVED: Resolved
            - ESCALATED: Escalated to higher authority
            - CLOSED: Closed

            Media Types:
            - IMAGE: Image file
            - VIDEO: Video file
            - AUDIO: Audio file
            - DOCUMENT: Document file

            Examples:
            GET /api/incidents/123e4567-e89b-12d3-a456-426614174000
        ");
    }
}
