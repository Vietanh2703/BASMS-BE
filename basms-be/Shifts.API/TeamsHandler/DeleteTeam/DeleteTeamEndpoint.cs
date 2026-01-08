using Shifts.API.Utilities;

namespace Shifts.API.TeamsHandler.DeleteTeam;

public class DeleteTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/shifts/teams/{id:guid}/delete", async (Guid id, ISender sender, HttpContext context) =>
        {
            var command = new DeleteTeamCommand(
                TeamId: id,
                DeletedBy: context.GetUserIdFromContext()
            );

            var result = await sender.Send(command);

            if (!result.Success)
            {
                return Results.BadRequest(new { success = false, message = result.Message });
            }

            return Results.Ok(new { success = true, message = result.Message });
        })
        .AddStandardDeleteDocumentation<object>(
            tag: "Teams",
            name: "DeleteTeam",
            summary: "Soft delete a team (PUT method)");
    }
}
