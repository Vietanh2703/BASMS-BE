namespace Shifts.API.ShiftsHandler.UpdateShift;

/// <summary>
/// Request DTO từ client
/// </summary>
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
        // Route: PUT /shifts/{id}
        app.MapPut("/api/shifts/{id:guid}", async (Guid id, UpdateShiftRequest req, ISender sender, HttpContext context) =>
        {
            // Lấy userId từ claims
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                userId = Guid.NewGuid(); // Fallback for testing
            }

            // Map request DTO sang command
            var command = new UpdateShiftCommand(
                ShiftId: id,
                ShiftDate: req.ShiftDate,
                StartTime: req.StartTime,
                EndTime: req.EndTime,
                RequiredGuards: req.RequiredGuards,
                Description: req.Description,
                UpdatedBy: userId
            );

            // Gửi command đến handler
            var result = await sender.Send(command);

            // Trả về 200 OK
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Shifts")
        .WithName("UpdateShift")
        .Produces<UpdateShiftResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Update an existing shift")
        .WithDescription(@"Updates shift information with re-validation.
            If shift date changes:
            - Re-checks public holiday status
            - Re-checks if location is closed
            - Updates all date-related fields");
    }
}
