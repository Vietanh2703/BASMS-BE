namespace Shifts.API.ShiftsHandler.GenerateShifts;

public class GenerateShiftsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/generate", async (GenerateShiftsRequest request, ISender sender) =>
        {
            var command = new GenerateShiftsCommand(
                ManagerId: request.ManagerId,
                ShiftTemplateIds: request.ShiftTemplateIds,
                GenerateFromDate: request.GenerateFromDate,
                GenerateDays: request.GenerateDays ?? 30
            );

            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .AddStandardPostDocumentation<GenerateShiftsResult>(
            tag: "Shifts - Auto Generation",
            name: "GenerateShifts",
            summary: "Generate shifts from shift template");
    }
}

public record GenerateShiftsRequest
{
    public Guid ManagerId { get; init; }
    public List<Guid> ShiftTemplateIds { get; init; } = new();
    public DateTime? GenerateFromDate { get; init; }
    public int? GenerateDays { get; init; }
}
