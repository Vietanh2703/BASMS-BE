namespace Incidents.API.IncidentHandler.GetAllIncidentsByReporterId;

public class GetAllIncidentsByReporterIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/incidents/reporter/{reporterId:guid}", async (
            Guid reporterId,
            ISender sender,
            ILogger<GetAllIncidentsByReporterIdEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/incidents/reporter/{ReporterId} - Getting incidents by reporter",
                reporterId);

            var query = new GetAllIncidentsByReporterIdQuery(reporterId);

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to get incidents for reporter {ReporterId}: {Error}",
                    reporterId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "Retrieved {Count} incidents for reporter {ReporterId}",
                result.Incidents.Count,
                reporterId);

            return Results.Ok(new
            {
                success = true,
                data = result.Incidents,
                totalCount = result.TotalCount,
                reporterId = result.ReporterId,
                message = $"Found {result.TotalCount} incidents sorted by incident time (newest first)"
            });
        })
        .RequireAuthorization()
        .WithName("GetAllIncidentsByReporterId")
        .WithTags("Incidents")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .WithSummary("Get all incidents by reporter ID");
    }
}
