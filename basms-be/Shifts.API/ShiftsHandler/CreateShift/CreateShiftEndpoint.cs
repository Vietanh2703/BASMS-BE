namespace Shifts.API.ShiftsHandler.CreateShift;

/// <summary>
/// Request DTO từ client
/// </summary>
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
        // Route: POST /shifts
        app.MapPost("/shifts", async (CreateShiftRequest req, ISender sender, HttpContext context) =>
        {
            // Lấy userId từ claims (giả sử đã authenticate)
            var userIdClaim = context.User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                // Fallback for testing
                userId = Guid.NewGuid();
            }

            // Map request DTO sang command
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

            // Gửi command đến handler
            var result = await sender.Send(command);

            // Trả về 201 Created với shift ID
            return Results.Created($"/shifts/{result.ShiftId}", result);
        })
        .WithTags("Shifts")
        .WithName("CreateShift")
        .Produces<CreateShiftResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Create a new shift")
        .WithDescription(@"Creates a new shift with validation from Contracts.API.
            Validates:
            - Contract exists and is active (if ContractId provided)
            - Location belongs to contract and is active
            - Checks if date is public holiday
            - Checks if location is closed on that date");
    }
}
