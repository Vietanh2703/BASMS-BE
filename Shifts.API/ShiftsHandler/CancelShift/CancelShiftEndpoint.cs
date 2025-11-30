namespace Shifts.API.ShiftsHandler.CancelShift;

/// <summary>
/// Request DTO từ client
/// </summary>
public record CancelShiftRequest(
    Guid ShiftId,
    string CancellationReason
);

public class CancelShiftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /shifts/{shiftId}/cancel
        app.MapPost("/api/shifts/{shiftId}/cancel", async (
            Guid shiftId,
            CancelShiftRequest req,
            ISender sender,
            HttpContext context) =>
        {
            // Lấy userId từ claims (giả sử đã authenticate)
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                // Fallback for testing
                userId = Guid.NewGuid();
            }

            // Validate cancellation reason
            if (string.IsNullOrWhiteSpace(req.CancellationReason))
            {
                return Results.BadRequest(new
                {
                    error = "Cancellation reason is required",
                    message = "Vui lòng cung cấp lý do hủy ca trực"
                });
            }

            // Map request DTO sang command
            var command = new CancelShiftCommand(
                ShiftId: shiftId,
                CancellationReason: req.CancellationReason,
                CancelledBy: userId
            );

            // Gửi command đến handler
            var result = await sender.Send(command);

            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    error = result.Message,
                    affectedGuards = result.AffectedGuards
                });
            }

            // Trả về 200 OK với kết quả
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Shifts")
        .WithName("CancelShift")
        .Produces<CancelShiftResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Cancel a shift")
        .WithDescription(@"Cancels a shift and notifies all assigned guards.
            Process:
            1. Updates shift status to CANCELLED
            2. Updates all shift assignments to CANCELLED
            3. Sends in-app notifications to all affected guards
            4. Sends cancellation emails to guards (if email available)
            5. Sends notifications to director and customer

            Restrictions:
            - Cannot cancel already cancelled shifts
            - Cannot cancel completed shifts");
    }
}
