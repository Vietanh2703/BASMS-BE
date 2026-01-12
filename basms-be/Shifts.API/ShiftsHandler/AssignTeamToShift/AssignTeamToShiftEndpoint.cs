using Shifts.API.Utilities;

namespace Shifts.API.ShiftsHandler.AssignTeamToShift;

public record AssignTeamToShiftRequest(
    DateTime StartDate,
    DateTime EndDate,
    string ShiftTimeSlot,
    Guid LocationId,
    Guid? ContractId,
    string AssignmentType,
    string? AssignmentNotes
);

public class AssignTeamToShiftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/teams/{teamId}/assign", async (
            Guid teamId,
            AssignTeamToShiftRequest req,
            ISender sender,
            HttpContext context) =>
        {
            var validTimeSlots = new[] { "MORNING", "AFTERNOON", "EVENING" };
            if (!validTimeSlots.Contains(req.ShiftTimeSlot.ToUpper()))
            {
                return Results.BadRequest(new
                {
                    error = "Invalid ShiftTimeSlot",
                    message = $"ShiftTimeSlot phải là một trong: {string.Join(", ", validTimeSlots)}",
                    received = req.ShiftTimeSlot
                });
            }

            if (req.EndDate < req.StartDate)
            {
                return Results.BadRequest(new
                {
                    error = "Invalid date range",
                    message = "EndDate phải lớn hơn hoặc bằng StartDate"
                });
            }

            var command = new AssignTeamToShiftCommand(
                TeamId: teamId,
                StartDate: req.StartDate.Date,
                EndDate: req.EndDate.Date,
                ShiftTimeSlot: req.ShiftTimeSlot.ToUpper(),
                LocationId: req.LocationId,
                ContractId: req.ContractId,
                AssignmentType: req.AssignmentType.ToUpper(),
                AssignmentNotes: req.AssignmentNotes,
                AssignedBy: context.GetUserIdFromContext()
            );

            var result = await sender.Send(command);
            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    errors = result.Errors,
                    warnings = result.Warnings
                });
            }
            return Results.Ok(result);
        })
        .AddStandardPostDocumentation<AssignTeamToShiftResult>(
            tag: "Shifts",
            name: "AssignTeamToShift",
            summary: "Assign team to shifts (multi-day)");
    }
}
