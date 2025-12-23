namespace Shifts.API.ShiftsHandler.CancelShift;

public record CancelShiftRequest(
    Guid ShiftId,
    string CancellationReason
);

public class CancelShiftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/{shiftId}/cancel", async (
            Guid shiftId,
            CancelShiftRequest req,
            ISender sender,
            HttpContext context) =>
        {
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                userId = Guid.NewGuid();
            }
            
            if (string.IsNullOrWhiteSpace(req.CancellationReason))
            {
                return Results.BadRequest(new
                {
                    error = "Cancellation reason is required",
                    message = "Vui lòng cung cấp lý do hủy ca trực"
                });
            }
            
            var command = new CancelShiftCommand(
                ShiftId: shiftId,
                CancellationReason: req.CancellationReason,
                CancelledBy: userId
            );
            
            var result = await sender.Send(command);

            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    error = result.Message,
                    affectedGuards = result.AffectedGuards
                });
            }
            
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Shifts")
        .WithName("CancelShift")
        .Produces<CancelShiftResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Cancel a shift");
    }
}
