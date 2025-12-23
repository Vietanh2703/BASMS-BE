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
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                userId = Guid.NewGuid();
            }

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
                AssignedBy: userId
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
        .RequireAuthorization()
        .WithTags("Shifts", "Teams")
        .WithName("AssignTeamToShift")
        .Produces<AssignTeamToShiftResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Assign team to shifts (multi-day)");
    }
}
