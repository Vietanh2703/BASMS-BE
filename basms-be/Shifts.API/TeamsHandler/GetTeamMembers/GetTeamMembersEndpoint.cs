namespace Shifts.API.TeamsHandler.GetTeamMembers;

public class GetTeamMembersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/teams/{teamId}/members", async (
            Guid teamId,
            ISender sender,
            ILogger<GetTeamMembersEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("GetTeamMembers request: {TeamId}", teamId);

                var query = new GetTeamMembersQuery(teamId);
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    return Results.NotFound(new ProblemDetails
                    {
                        Title = "Team not found or error",
                        Detail = result.ErrorMessage,
                        Status = StatusCodes.Status404NotFound
                    });
                }

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting team members for {TeamId}", teamId);
                return Results.Problem(
                    title: "Error getting team members",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .AddStandardGetDocumentation<GetTeamMembersResult>(
            tag: "Teams",
            name: "GetTeamMembers",
            summary: "Lấy danh sách members của team",
            requiresAuth: false);
    }
}
