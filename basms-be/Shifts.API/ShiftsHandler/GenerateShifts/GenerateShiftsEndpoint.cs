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
        .WithTags("Shifts - Auto Generation")
        .WithName("GenerateShifts")
        .Produces<GenerateShiftsResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Generate shifts from shift template");
    }
}

public record GenerateShiftsRequest
{
    public Guid ManagerId { get; init; }
    public List<Guid> ShiftTemplateIds { get; init; } = new();
    public DateTime? GenerateFromDate { get; init; }
    public int? GenerateDays { get; init; }
}
