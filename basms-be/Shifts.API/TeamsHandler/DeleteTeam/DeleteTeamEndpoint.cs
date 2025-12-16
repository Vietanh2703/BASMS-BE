namespace Shifts.API.TeamsHandler.DeleteTeam;

public class DeleteTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: DELETE /api/shifts/teams/{id}
        app.MapPut("/api/shifts/teams/{id:guid}", async (Guid id, ISender sender, HttpContext context) =>
        {
            // Lấy userId từ claims
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                userId = Guid.NewGuid(); // Fallback for testing
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
        .WithSummary("Delete a team (soft delete)")
        .WithDescription(@"Soft deletes a team by setting IsDeleted = 1.

            Validates:
            - Team exists and is not already deleted
            - Team has NOT been assigned to any shifts (checks shift_assignments table)
            - Team has no active members

            If team has shift assignments, the delete operation will be rejected with a descriptive error message.

            Upon successful deletion:
            - Sets IsDeleted = true for the team
            - Decrements TotalTeamManaged counter for the manager");
    }
}
