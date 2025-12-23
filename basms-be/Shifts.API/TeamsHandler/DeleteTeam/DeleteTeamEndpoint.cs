namespace Shifts.API.TeamsHandler.DeleteTeam;

public class DeleteTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/shifts/teams/{id:guid}/delete", async (Guid id, ISender sender, HttpContext context) =>
        {
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                userId = Guid.NewGuid();
            }
            
            var command = new DeleteTeamCommand(
                TeamId: id,
                DeletedBy: userId
            );
            
            var result = await sender.Send(command);
            
            if (!result.Success)
            {
                return Results.BadRequest(new { success = false, message = result.Message });
            }

            return Results.Ok(new { success = true, message = result.Message });
        })
        .RequireAuthorization()
        .WithTags("Teams")
        .WithName("DeleteTeam")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Soft delete a team (PUT method)");
    }
}
