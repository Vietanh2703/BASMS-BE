using Shifts.API.Utilities;

namespace Shifts.API.ShiftsHandler.GetShiftIssueByGuardId;

public class GetShiftIssueByGuardIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/guards/{guardId:guid}/shift-issues", async (
            [FromRoute] Guid guardId,
            ISender sender,
            ILogger<GetShiftIssueByGuardIdEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("GET /api/guards/{GuardId}/shift-issues - Getting shift issues for guard", guardId);

            var query = new GetShiftIssueByGuardIdQuery(guardId);
            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning("Failed to get shift issues for Guard {GuardId}: {Error}", guardId, result.ErrorMessage);
                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation("Found {Count} shift issues for Guard {GuardId}", result.TotalIssues, guardId);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    guardId = result.GuardId,
                    issues = result.Issues,
                    totalIssues = result.TotalIssues
                }
            });
        })
        .AddStandardGetDocumentation<object>(
            tag: "Guards - Issues",
            name: "GetShiftIssueByGuardId",
            summary: "Lấy danh sách shift issues của guard");
    }
}
