namespace Shifts.API.ShiftsHandler.CustomerViewShift;

public class CustomerViewShiftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/customer/{contractId}/view", async (
            Guid contractId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? status,
            [FromQuery] string? shiftType,
            ISender sender,
            ILogger<CustomerViewShiftEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/shifts/customer/{ContractId}/view - Customer viewing contract shifts",
                contractId);

            var query = new CustomerViewShiftQuery(
                ContractId: contractId,
                FromDate: fromDate,
                ToDate: toDate,
                Status: status,
                ShiftType: shiftType
            );

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to get shifts for contract {ContractId}: {Error}",
                    contractId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "Retrieved {Count} shifts for contract {ContractId}",
                result.Shifts.Count,
                contractId);

            return Results.Ok(new
            {
                success = true,
                data = result.Shifts,
                totalCount = result.TotalCount,
                summary = result.Summary,
                message = "Danh sách ca trực được sắp xếp theo ngày và giờ",
                filters = new
                {
                    contractId = contractId.ToString(),
                    fromDate = fromDate?.ToString("yyyy-MM-dd") ?? "all",
                    toDate = toDate?.ToString("yyyy-MM-dd") ?? "all",
                    status = status ?? "all",
                    shiftType = shiftType ?? "all"
                }
            });
        })
        .RequireAuthorization()
        .WithName("CustomerViewShift")
        .WithTags("Shifts", "Customer")
        .Produces(200)
        .Produces(400)
        .WithSummary("Customer xem các ca trực của contract");
    }
}
