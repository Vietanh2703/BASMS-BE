using Shifts.API.Utilities;

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
            var command = new CreateShiftCommand(
                ContractId: req.ContractId,
                LocationId: req.LocationId,
                ShiftDate: req.ShiftDate,
                StartTime: req.StartTime,
                EndTime: req.EndTime,
                RequiredGuards: req.RequiredGuards,
                ShiftType: req.ShiftType,
                Description: req.Description,
                CreatedBy: context.GetUserIdFromContext()
            );

            var result = await sender.Send(command);
            return Results.Created($"/shifts/{result.ShiftId}", result);
        })
        .AddStandardPostDocumentation<CreateShiftResult>(
            tag: "Shifts",
            name: "CreateShift",
            summary: "Create a new shift");
    }
}
