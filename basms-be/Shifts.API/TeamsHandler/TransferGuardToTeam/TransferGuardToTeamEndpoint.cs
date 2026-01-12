using Shifts.API.Utilities;

namespace Shifts.API.TeamsHandler.TransferGuardToTeam;

public record TransferGuardToTeamRequest(
    Guid GuardId,
    string Role,
    string? TransferNotes
);

public class TransferGuardToTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/teams/{teamId}/transfer-guard", async (
            Guid teamId,
            TransferGuardToTeamRequest req,
            ISender sender,
            HttpContext context) =>
        {
            var command = new TransferGuardToTeamCommand(
                TargetTeamId: teamId,
                GuardId: req.GuardId,
                Role: req.Role,
                TransferNotes: req.TransferNotes,
                TransferredBy: context.GetUserIdFromContext()
            );

            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .AddStandardPostDocumentation<TransferGuardToTeamResult>(
            tag: "Teams",
            name: "TransferGuardToTeam",
            summary: "Transfer guard from current team to new team");
    }
}
