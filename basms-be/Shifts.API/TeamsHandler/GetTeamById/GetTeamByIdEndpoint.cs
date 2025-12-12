using Microsoft.AspNetCore.Mvc;

namespace Shifts.API.TeamsHandler.GetTeamById;

public class GetTeamByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/teams/{teamId}", GetTeamByIdAsync)
            .WithName("GetTeamById")
            .WithTags("Teams")
            .WithSummary("Lấy thông tin chi tiết team")
            .WithDescription(@"
Lấy thông tin chi tiết team bao gồm:
- Thông tin team (code, name, manager, specialization, etc.)
- Danh sách members trong team với thông tin:
  - Guard info (name, employee code, certification level)
  - Role trong team (LEADER/DEPUTY/MEMBER)
  - Performance metrics (shifts assigned/completed, attendance rate)

Members được sắp xếp theo thứ tự: LEADER → DEPUTY → MEMBER
            ")
            .Produces<GetTeamByIdResult>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> GetTeamByIdAsync(
        Guid teamId,
        ISender sender,
        ILogger<GetTeamByIdEndpoint> logger)
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
    }
}
