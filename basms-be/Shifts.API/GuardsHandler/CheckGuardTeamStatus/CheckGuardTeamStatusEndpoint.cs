namespace Shifts.API.GuardsHandler.CheckGuardTeamStatus;

public class CheckGuardTeamStatusEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/guards/{guardId}/team-status", async (
            Guid guardId,
            ISender sender) =>
        {
            var query = new CheckGuardTeamStatusQuery(guardId);
            var result = await sender.Send(query);

            return Results.Ok(result);
        })
        .AddStandardGetDocumentation<CheckGuardTeamStatusResult>(
            tag: "Guards",
            name: "CheckGuardTeamStatus",
            summary: "Check if guard belongs to any active team");
    }
}
