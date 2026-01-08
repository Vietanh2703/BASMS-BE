namespace Shifts.API.TeamsHandler.UpdateTeam;

public record UpdateTeamRequest(
    string? TeamName,
    string? Specialization,
    string? Description,
    int? MinMembers,
    int? MaxMembers
);

public class UpdateTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/shifts/teams/{id:guid}", async (Guid id, UpdateTeamRequest req, ISender sender, HttpContext context) =>
        {
            var command = new UpdateTeamCommand(
                TeamId: id,
                TeamName: req.TeamName,
                Specialization: req.Specialization,
                Description: req.Description,
                MinMembers: req.MinMembers,
                MaxMembers: req.MaxMembers,
                UpdatedBy: context.GetUserIdFromContext()
            );

            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .AddStandardPutDocumentation<UpdateTeamResult>(
            tag: "Teams",
            name: "UpdateTeam",
            summary: "Update an existing team");
    }
}
