namespace Shifts.API.TeamsHandler.UpdateTeam;

/// <summary>
/// Request DTO từ client
/// </summary>
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
        // Route: PUT /api/shifts/teams/{id}
        app.MapPut("/api/shifts/teams/{id:guid}", async (Guid id, UpdateTeamRequest req, ISender sender, HttpContext context) =>
        {
            // Lấy userId từ claims
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                userId = Guid.NewGuid(); // Fallback for testing
            }

            // Map request DTO sang command
            var command = new UpdateTeamCommand(
                TeamId: id,
                TeamName: req.TeamName,
                Specialization: req.Specialization,
                Description: req.Description,
                MinMembers: req.MinMembers,
                MaxMembers: req.MaxMembers,
                UpdatedBy: userId
            );

            // Gửi command đến handler
            var result = await sender.Send(command);

            // Trả về 200 OK
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Teams")
        .WithName("UpdateTeam")
        .Produces<UpdateTeamResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Update an existing team")
        .WithDescription(@"Updates team information with validation.
            Validates:
            - Team exists and is not deleted
            - TeamName is not empty (if provided)
            - MinMembers >= 1 (if provided)
            - MaxMembers >= MinMembers (if provided)
            - Specialization is valid (if provided)
            - If reducing MaxMembers, current member count must not exceed new MaxMembers");
    }
}
