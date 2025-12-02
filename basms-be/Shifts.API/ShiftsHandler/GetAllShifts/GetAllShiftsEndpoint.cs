namespace Shifts.API.ShiftsHandler.GetAllShifts;

/// <summary>
/// Endpoint để lấy danh sách tất cả shifts với filtering và pagination
/// </summary>
public class GetAllShiftsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/get-all", async (
    [FromQuery] DateTime? fromDate,
    [FromQuery] DateTime? toDate,
    [FromQuery] Guid? managerId,
    [FromQuery] Guid? locationId,
    [FromQuery] Guid? contractId,
    [FromQuery] Guid? shiftTemplateId,  
    [FromQuery] string? status,
    [FromQuery] string? shiftType,
    [FromQuery] bool? isNightShift,
    ISender sender,
    ILogger<GetAllShiftsEndpoint> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation(
        "GET /api/shifts - Getting all shifts with filters");

    var query = new GetAllShiftsQuery(
        FromDate: fromDate,
        ToDate: toDate,
        ManagerId: managerId,
        LocationId: locationId,
        ContractId: contractId,
        ShiftTemplateId: shiftTemplateId, 
        Status: status,
        ShiftType: shiftType,
        IsNightShift: isNightShift
    );

    var result = await sender.Send(query, cancellationToken);

    if (!result.Success)
    {
        logger.LogWarning(
            "Failed to get shifts: {Error}",
            result.ErrorMessage);

        return Results.BadRequest(new
        {
            success = false,
            error = result.ErrorMessage
        });
    }

    logger.LogInformation(
        "✓ Retrieved {Count} shifts sorted by date and shift time",
        result.Shifts.Count);

    return Results.Ok(new
    {
        success = true,
        data = result.Shifts,
        totalCount = result.TotalCount,
        message = "Shifts sorted by date (ascending) → shift time (morning → afternoon → evening)",
        filters = new
        {
            fromDate = fromDate?.ToString("yyyy-MM-dd") ?? "all",
            toDate = toDate?.ToString("yyyy-MM-dd") ?? "all",
            managerId = managerId?.ToString() ?? "all",
            locationId = locationId?.ToString() ?? "all",
            contractId = contractId?.ToString() ?? "all",
            shiftTemplateId = shiftTemplateId?.ToString() ?? "all",
            status = status ?? "all",
            shiftType = shiftType ?? "all",
            isNightShift = isNightShift?.ToString() ?? "all"
        }
    });
})
        // .RequireAuthorization()
        .WithName("GetAllShifts")
        .WithTags("Shifts")
        .Produces(200)
        .Produces(400)
        .WithSummary("Get all shifts with filtering")
        .WithDescription(@"
            Returns all shifts sorted by date (ascending) and shift time (morning → afternoon → evening).

            Sorting logic:
            - First: By date (earliest to latest)
            - Then: By shift start time (morning shifts first, then afternoon, then evening/night)

            Query Parameters:
            - fromDate (optional): Filter shifts from this date (yyyy-MM-dd)
            - toDate (optional): Filter shifts until this date (yyyy-MM-dd)
            - managerId (optional): Filter by manager ID
            - locationId (optional): Filter by location ID
            - contractId (optional): Filter by contract ID
            - status (optional): Filter by status (DRAFT, SCHEDULED, IN_PROGRESS, COMPLETED, CANCELLED, PARTIAL)
            - shiftType (optional): Filter by shift type (REGULAR, OVERTIME, EMERGENCY, REPLACEMENT, TRAINING)
            - isNightShift (optional): Filter by night shift (true/false)
            - shiftTemplateId (optional): Filter by shift template ID

            Examples:
            GET /api/shifts/get-all
            GET /api/shifts/get-all?fromDate=2025-01-01&toDate=2025-01-31
            GET /api/shifts/get-all?managerId={guid}
            GET /api/shifts/get-all?locationId={guid}&status=SCHEDULED
            GET /api/shifts/get-all?fromDate=2025-01-01&status=COMPLETED
            GET /api/shifts/get-all?contractId={guid}&isNightShift=true
            GET /api/shifts/get-all?shiftTemplateId={guid}
        ");
    }
}
