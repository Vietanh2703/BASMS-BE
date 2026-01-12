using Shifts.API.Utilities;

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
            var command = new UpdateShiftCommand(
                ShiftId: id,
                ShiftDate: req.ShiftDate,
                StartTime: req.StartTime,
                EndTime: req.EndTime,
                RequiredGuards: req.RequiredGuards,
                Description: req.Description,
                UpdatedBy: context.GetUserIdFromContext()
            );

            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .AddStandardPutDocumentation<UpdateShiftResult>(
            tag: "Shifts",
            name: "UpdateShift",
            summary: "Update an existing shift");
    }
}
