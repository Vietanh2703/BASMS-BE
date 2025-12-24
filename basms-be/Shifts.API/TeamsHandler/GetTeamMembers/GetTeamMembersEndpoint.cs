namespace Shifts.API.TeamsHandler.GetTeamMembers;

public class GetTeamMembersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/teams/{teamId}/members", GetTeamMembersAsync)
            .WithName("GetTeamMembers")
            .WithTags("Teams")
            .WithSummary("Lấy danh sách members của team")
            .Produces<GetTeamMembersResult>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> GetTeamMembersAsync(
        Guid teamId,
        ISender sender,
        ILogger<GetTeamMembersEndpoint> logger)
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
    }
}
