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
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                userId = Guid.NewGuid(); 
            }
            
            var command = new UpdateTeamCommand(
                TeamId: id,
                TeamName: req.TeamName,
                Specialization: req.Specialization,
                Description: req.Description,
                MinMembers: req.MinMembers,
                MaxMembers: req.MaxMembers,
                UpdatedBy: userId
            );
            
            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Teams")
        .WithName("UpdateTeam")
        .Produces<UpdateTeamResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Update an existing team");
    }
}
