using Shifts.API.Utilities;

namespace Shifts.API.ShiftsHandler.GetAllShifts;

public class GetAllShiftsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/get-all", async (
            [FromQuery] Guid? contractId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] Guid? managerId,
            [FromQuery] Guid? locationId,
            [FromQuery] string? status,
            [FromQuery] string? shiftType,
            [FromQuery] bool? isNightShift,
            ISender sender,
            ILogger<GetAllShiftsEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("GET /api/shifts/get-all - Getting all shifts for contract");

            var query = new GetAllShiftsQuery(
                ContractId: contractId,
                FromDate: fromDate,
                ToDate: toDate,
                ManagerId: managerId,
                LocationId: locationId,
                Status: status,
                ShiftType: shiftType,
                IsNightShift: isNightShift
            );

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning("Failed to get shifts: {Error}", result.ErrorMessage);
                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation("Retrieved {Count} shifts sorted by date and shift time", result.Shifts.Count);

            return Results.Ok(new
            {
                success = true,
                data = result.Shifts,
                totalCount = result.TotalCount,
                message = "Shifts sorted by date (ascending) → shift time (morning → afternoon → evening)",
                note = "One contract can have multiple shift templates (multiple shifts per day)",
                filters = new
                {
                    contractId = contractId?.ToString() ?? "all",
                    fromDate = fromDate?.ToString("yyyy-MM-dd") ?? "all",
                    toDate = toDate?.ToString("yyyy-MM-dd") ?? "all",
                    managerId = managerId?.ToString() ?? "all",
                    locationId = locationId?.ToString() ?? "all",
                    status = status ?? "all",
                    shiftType = shiftType ?? "all",
                    isNightShift = isNightShift?.ToString() ?? "all"
                }
            });
        })
        .AddStandardGetDocumentation<object>(
            tag: "Shifts",
            name: "GetAllShifts",
            summary: "Get all shifts by contract with filtering",
            requiresAuth: false,
            canReturnNotFound: false);
    }
}
