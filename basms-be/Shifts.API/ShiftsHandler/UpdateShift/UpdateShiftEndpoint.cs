namespace Shifts.API.ShiftsHandler.UpdateShift;

public record UpdateShiftRequest(
    DateTime? ShiftDate,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    int? RequiredGuards,
    string? Description
);

public class UpdateShiftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/shifts/{id:guid}", async (Guid id, UpdateShiftRequest req, ISender sender, HttpContext context) =>
        {
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                userId = Guid.NewGuid(); 
            }
            
            var command = new UpdateShiftCommand(
                ShiftId: id,
                ShiftDate: req.ShiftDate,
                StartTime: req.StartTime,
                EndTime: req.EndTime,
                RequiredGuards: req.RequiredGuards,
                Description: req.Description,
                UpdatedBy: userId
            );
            
            var result = await sender.Send(command);
            
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Shifts")
        .WithName("UpdateShift")
        .Produces<UpdateShiftResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Update an existing shift");
    }
}
