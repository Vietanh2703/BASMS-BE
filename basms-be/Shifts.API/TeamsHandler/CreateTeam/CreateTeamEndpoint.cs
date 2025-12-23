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
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                userId = Guid.NewGuid();
            }
            
            var command = new CreateTeamCommand(
                ManagerId: req.ManagerId,
                TeamName: req.TeamName,
                Specialization: req.Specialization,
                Description: req.Description,
                MinMembers: req.MinMembers,
                MaxMembers: req.MaxMembers,
                CreatedBy: userId
            );
            
            var result = await sender.Send(command);

            return Results.Created($"/teams/{result.TeamId}", result);
        })
        .RequireAuthorization()
        .WithTags("Teams")
        .WithName("CreateTeam")
        .Produces<CreateTeamResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Create a new team");
    }
}
