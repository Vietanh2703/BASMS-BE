namespace Shifts.API.TeamsHandler.AddMemberToTeam;


public record AddMemberToTeamRequest(
    Guid GuardId,
    string Role,
    string? JoiningNotes
);

public class AddMemberToTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/teams/{teamId}/members", async (Guid teamId, AddMemberToTeamRequest req, ISender sender, HttpContext context) =>
        {
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                userId = Guid.NewGuid();
            }


            var command = new AddMemberToTeamCommand(
                TeamId: teamId,
                GuardId: req.GuardId,
                Role: req.Role,
                JoiningNotes: req.JoiningNotes,
                CreatedBy: userId
            );
            
            var result = await sender.Send(command);
            
            return Results.Created($"/teams/{teamId}/members/{result.TeamMemberId}", result);
        })
        .RequireAuthorization()
        .WithTags("Teams")
        .WithName("AddMemberToTeam")
        .Produces<AddMemberToTeamResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Add guard to team");
    }
}
