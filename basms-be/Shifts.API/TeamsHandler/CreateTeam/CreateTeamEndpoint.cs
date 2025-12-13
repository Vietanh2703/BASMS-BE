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
        // Route: POST /api/shifts/teams
        app.MapPost("/api/shifts/teams", async (CreateTeamRequest req, ISender sender, HttpContext context) =>
        {
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                // Fallback for testing
                userId = Guid.NewGuid();
            }

            // Map request DTO sang command
            var command = new CreateTeamCommand(
                ManagerId: req.ManagerId,
                TeamName: req.TeamName,
                Specialization: req.Specialization,
                Description: req.Description,
                MinMembers: req.MinMembers,
                MaxMembers: req.MaxMembers,
                CreatedBy: userId
            );

            // Gửi command đến handler
            var result = await sender.Send(command);

            // Trả về 201 Created với team ID
            return Results.Created($"/teams/{result.TeamId}", result);
        })
        .RequireAuthorization()
        .WithTags("Teams")
        .WithName("CreateTeam")
        .Produces<CreateTeamResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Create a new team")
        .WithDescription(@"Creates a new team with validation.
            Validates:
            - Manager exists and is active
            - TeamName is not empty
            - MinMembers >= 1
            - MaxMembers >= MinMembers (if provided)
            - Specialization is valid (RESIDENTIAL, COMMERCIAL, EVENT, VIP, INDUSTRIAL)

            Team code is auto-generated: T-xxxxxx (6 random digits)");
    }
}
