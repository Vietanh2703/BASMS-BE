namespace Shifts.API.TeamsHandler.DeleteTeam;

public class DeleteTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: PUT /api/shifts/teams/{id}
        app.MapPut("/api/shifts/teams/{id:guid}/delete", async (Guid id, ISender sender, HttpContext context) =>
        {
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                userId = Guid.NewGuid();
            }

            // Tạo command
            var command = new DeleteTeamCommand(
                TeamId: id,
                DeletedBy: userId
            );

            // Gửi command đến handler
            var result = await sender.Send(command);

            // Trả về response
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
        .WithSummary("Soft delete a team (PUT method)")
        .WithDescription(@"Soft deletes a team by setting IsDeleted from 0 to 1.

            Input: TeamId (GUID) from route parameter

            Validation:
            - Team exists and is not already deleted
            - Team has NOT been assigned to any active shifts (checks shift_assignments table where IsDeleted = 0)

            If team has been assigned to any shifts, the operation will be rejected with an error message.

            Upon successful deletion:
            - Sets IsDeleted = 1 (true) for the team
            - Sets DeletedAt and DeletedBy timestamps
            - Decrements TotalTeamManaged counter for the manager");
    }
}
