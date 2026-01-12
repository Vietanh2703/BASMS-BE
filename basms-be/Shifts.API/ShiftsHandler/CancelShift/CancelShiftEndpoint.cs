using Shifts.API.Utilities;

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
                CancelledBy: context.GetUserIdFromContext()
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
        .AddStandardPostDocumentation<CancelShiftResult>(
            tag: "Shifts",
            name: "CancelShift",
            summary: "Cancel a shift");
    }
}
