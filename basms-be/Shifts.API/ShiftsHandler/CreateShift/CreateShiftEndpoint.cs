namespace Shifts.API.ShiftsHandler.CreateShift;

public record CreateShiftRequest(
    Guid? ContractId,
    Guid LocationId,
    DateTime ShiftDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int RequiredGuards,
    string ShiftType,
    string? Description
);

public class CreateShiftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts", async (CreateShiftRequest req, ISender sender, HttpContext context) =>
        {
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                userId = Guid.NewGuid();
            }
            
            var command = new CreateShiftCommand(
                ContractId: req.ContractId,
                LocationId: req.LocationId,
                ShiftDate: req.ShiftDate,
                StartTime: req.StartTime,
                EndTime: req.EndTime,
                RequiredGuards: req.RequiredGuards,
                ShiftType: req.ShiftType,
                Description: req.Description,
                CreatedBy: userId
            );
            
            var result = await sender.Send(command);
            return Results.Created($"/shifts/{result.ShiftId}", result);
        })
        .RequireAuthorization()
        .WithTags("Shifts")
        .WithName("CreateShift")
        .Produces<CreateShiftResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Create a new shift");
    }
}
