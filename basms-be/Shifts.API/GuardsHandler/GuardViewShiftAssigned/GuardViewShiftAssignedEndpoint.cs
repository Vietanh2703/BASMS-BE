using Shifts.API.Utilities;

namespace Shifts.API.GuardsHandler.GuardViewShiftAssigned;

public class GuardViewShiftAssignedEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/guards/{guardId}/assigned", async (
            Guid guardId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? status,
            ISender sender,
            ILogger<GuardViewShiftAssignedEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("GET /api/shifts/guards/{GuardId}/assigned - Guard viewing assigned shift schedule", guardId);

            var query = new GuardViewShiftAssignedQuery(
                GuardId: guardId,
                FromDate: fromDate,
                ToDate: toDate,
                Status: status
            );

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning("Failed to get assigned shifts for guard {GuardId}: {Error}", guardId, result.ErrorMessage);
                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation("Retrieved {Count} assigned shifts for guard {GuardId}", result.Shifts.Count, guardId);

            return Results.Ok(new
            {
                success = true,
                data = result.Shifts,
                totalCount = result.TotalCount,
                message = "Lịch ca trực được sắp xếp theo ngày và giờ",
                filters = new
                {
                    guardId = guardId.ToString(),
                    fromDate = fromDate?.ToString("yyyy-MM-dd") ?? "all",
                    toDate = toDate?.ToString("yyyy-MM-dd") ?? "all",
                    status = status ?? "all"
                }
            });
        })
        .AddStandardGetDocumentation<object>(
            tag: "Guards",
            name: "GuardViewShiftAssigned",
            summary: "Guard xem lịch ca trực được phân công");
    }
}
