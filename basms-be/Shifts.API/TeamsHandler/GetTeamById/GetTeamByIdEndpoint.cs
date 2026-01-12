using Shifts.API.Utilities;

namespace Shifts.API.TeamsHandler.GetTeamById;

public class GetTeamByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/teams/{teamId}", async (
            Guid teamId,
            ISender sender,
            ILogger<GetTeamByIdEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("GetTeamById request: {TeamId}", teamId);

                var query = new GetTeamByIdQuery(teamId);
                var result = await sender.Send(query);

                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Team {TeamId} not found", teamId);
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Team not found",
                    Detail = ex.Message,
                    Status = StatusCodes.Status404NotFound
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting team {TeamId}", teamId);
                return Results.Problem(
                    title: "Error getting team",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .AddStandardGetDocumentation<GetTeamByIdResult>(
            tag: "Teams",
            name: "GetTeamById",
            summary: "Lấy thông tin chi tiết team");
    }
}
