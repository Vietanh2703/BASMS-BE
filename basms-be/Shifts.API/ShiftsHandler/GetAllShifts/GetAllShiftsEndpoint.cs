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
            [FromQuery] string? status,
            [FromQuery] string? shiftType,
            [FromQuery] bool? isNightShift,
            [FromQuery] int pageNumber ,
            [FromQuery] int pageSize,
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
                Status: status,
                ShiftType: shiftType,
                IsNightShift: isNightShift,
                PageNumber: pageNumber,
                PageSize: pageSize
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
                "✓ Retrieved {Count} shifts (Total: {TotalCount}, Page: {Page}/{TotalPages})",
                result.Shifts.Count,
                result.TotalCount,
                result.PageNumber,
                result.TotalPages);

            return Results.Ok(new
            {
                success = true,
                data = result.Shifts,
                pagination = new
                {
                    totalCount = result.TotalCount,
                    pageNumber = result.PageNumber,
                    pageSize = result.PageSize,
                    totalPages = result.TotalPages,
                    hasPreviousPage = result.PageNumber > 1,
                    hasNextPage = result.PageNumber < result.TotalPages
                },
                filters = new
                {
                    fromDate = fromDate?.ToString("yyyy-MM-dd") ?? "all",
                    toDate = toDate?.ToString("yyyy-MM-dd") ?? "all",
                    managerId = managerId?.ToString() ?? "all",
                    locationId = locationId?.ToString() ?? "all",
                    contractId = contractId?.ToString() ?? "all",
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
        .WithSummary("Get all shifts with filtering and pagination")
        .WithDescription(@"
            Returns a paginated list of all shifts with optional filtering.

            Query Parameters:
            - fromDate (optional): Filter shifts from this date (yyyy-MM-dd)
            - toDate (optional): Filter shifts until this date (yyyy-MM-dd)
            - managerId (optional): Filter by manager ID
            - locationId (optional): Filter by location ID
            - contractId (optional): Filter by contract ID
            - status (optional): Filter by status (DRAFT, SCHEDULED, IN_PROGRESS, COMPLETED, CANCELLED, PARTIAL)
            - shiftType (optional): Filter by shift type (REGULAR, OVERTIME, EMERGENCY, REPLACEMENT, TRAINING)
            - isNightShift (optional): Filter by night shift (true/false)
            - pageNumber (optional): Page number (default: 1)
            - pageSize (optional): Page size (default: 50, max: 100)

            Examples:
            GET /api/shifts
            GET /api/shifts?fromDate=2025-01-01&toDate=2025-01-31
            GET /api/shifts?managerId={guid}
            GET /api/shifts?locationId={guid}&status=SCHEDULED
            GET /api/shifts?fromDate=2025-01-01&status=COMPLETED&pageNumber=1&pageSize=20
            GET /api/shifts?contractId={guid}&isNightShift=true
        ");
    }
}
