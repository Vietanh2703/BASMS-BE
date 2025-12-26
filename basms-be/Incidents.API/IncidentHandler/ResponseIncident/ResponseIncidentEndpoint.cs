namespace Incidents.API.IncidentHandler.ResponseIncident;

public class ResponseIncidentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/incidents/response", async (
            [FromBody] ResponseIncidentRequest request,
            ISender sender,
            ILogger<ResponseIncidentEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "POST /api/incidents/response - Responding to incident {IncidentId}",
                request.IncidentId);

            var command = new ResponseIncidentCommand(
                request.IncidentId,
                request.ResponderId,
                request.ResponseContent);

            var result = await sender.Send(command, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to respond to incident {IncidentId}: {Error}",
                    request.IncidentId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "✓ Incident {IncidentCode} responded successfully",
                result.IncidentCode);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    incidentId = result.IncidentId,
                    incidentCode = result.IncidentCode,
                    status = result.Status,
                    respondedAt = result.RespondedAt
                },
                message = result.Message
            });
        })
        .RequireAuthorization()
        .WithName("ResponseIncident")
        .WithTags("Incidents")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .WithSummary("Respond to an incident");
    }
}

public record ResponseIncidentRequest
{
    public Guid IncidentId { get; init; }
    public Guid ResponderId { get; init; }
    public string ResponseContent { get; init; } = string.Empty;
}
