namespace Shifts.API.TeamsHandler.AddMemberToTeam;

/// <summary>
/// Request DTO từ client
/// </summary>
public record AddMemberToTeamRequest(
    Guid GuardId,
    string Role,
    string? JoiningNotes
);

public class AddMemberToTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /api/shifts/teams/{teamId}/members
        app.MapPost("/api/shifts/teams/{teamId}/members", async (Guid teamId, AddMemberToTeamRequest req, ISender sender, HttpContext context) =>
        {
            // Lấy userId từ claims (giả sử đã authenticate)
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                // Fallback for testing
                userId = Guid.NewGuid();
            }

            // Map request DTO sang command
            var command = new AddMemberToTeamCommand(
                TeamId: teamId,
                GuardId: req.GuardId,
                Role: req.Role,
                JoiningNotes: req.JoiningNotes,
                CreatedBy: userId
            );

            // Gửi command đến handler
            var result = await sender.Send(command);

            // Trả về 201 Created với member ID
            return Results.Created($"/teams/{teamId}/members/{result.TeamMemberId}", result);
        })
        .RequireAuthorization()
        .WithTags("Teams")
        .WithName("AddMemberToTeam")
        .Produces<AddMemberToTeamResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Add guard to team")
        .WithDescription(@"Adds a guard to a team with validation.
            Validates:
            - Team exists and is active
            - Team has not exceeded MaxMembers
            - Guard exists, is active, and has valid EmploymentStatus (ACTIVE/PROBATION)
            - Guard is not already a member of this team
            - Role is valid (LEADER, DEPUTY, MEMBER)
            - LEADER/DEPUTY should have CertificationLevel II or III (warning if not)

            Role Guidelines:
            - LEADER: Team leader (should have Level II or III)
            - DEPUTY: Deputy leader (should have Level II or III)
            - MEMBER: Regular member (Level I or above)");
    }
}
