using Shifts.API.Utilities;

namespace Shifts.API.TeamsHandler.CreateTeam;

/// <summary>
/// Request DTO từ client
/// </summary>
public record CreateTeamRequest(
    Guid ManagerId,
    string TeamName,
    string? Specialization,
    string? Description,
    int MinMembers,
    int? MaxMembers
);

public class CreateTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/teams", async (CreateTeamRequest req, ISender sender, HttpContext context) =>
        {
            var command = new CreateTeamCommand(
                ManagerId: req.ManagerId,
                TeamName: req.TeamName,
                Specialization: req.Specialization,
                Description: req.Description,
                MinMembers: req.MinMembers,
                MaxMembers: req.MaxMembers,
                CreatedBy: context.GetUserIdFromContext()
            );

            var result = await sender.Send(command);
            return Results.Created($"/teams/{result.TeamId}", result);
        })
        .AddStandardPostDocumentation<CreateTeamResult>(
            tag: "Teams",
            name: "CreateTeam",
            summary: "Create a new team");
    }
}
