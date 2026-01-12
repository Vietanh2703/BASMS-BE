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
            var command = new AddMemberToTeamCommand(
                TeamId: teamId,
                GuardId: req.GuardId,
                Role: req.Role,
                JoiningNotes: req.JoiningNotes,
                CreatedBy: context.GetUserIdFromContext()
            );

            var result = await sender.Send(command);
            return Results.Created($"/teams/{teamId}/members/{result.TeamMemberId}", result);
        })
        .AddStandardPostDocumentation<AddMemberToTeamResult>(
            tag: "Teams",
            name: "AddMemberToTeam",
            summary: "Add guard to team");
    }
}
