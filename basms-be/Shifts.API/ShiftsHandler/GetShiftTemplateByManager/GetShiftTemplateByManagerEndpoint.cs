using Shifts.API.Utilities;

namespace Shifts.API.ShiftsHandler.GetShiftTemplateByManager;

public class GetShiftTemplateByManagerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/templates/by-manager/{managerId:guid}", async (
            [FromRoute] Guid managerId,
            [FromQuery] string? status,
            [FromQuery] bool? isActive,
            ISender sender,
            ILogger<GetShiftTemplateByManagerEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("GET /api/shifts/templates/by-manager/{ManagerId} - Getting shift templates", managerId);

            var query = new GetShiftTemplateByManagerQuery(
                ManagerId: managerId,
                Status: status,
                IsActive: isActive
            );

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning("Failed to get shift templates for Manager {ManagerId}: {Error}", managerId, result.ErrorMessage);
                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation("Found {Count} shift templates for Manager {ManagerId}", result.TotalCount, managerId);

            return Results.Ok(new
            {
                success = true,
                data = result.Templates,
                totalCount = result.TotalCount,
                filters = new
                {
                    managerId,
                    status = status ?? "all",
                    isActive = isActive?.ToString() ?? "all"
                }
            });
        })
        .AddStandardGetDocumentation<object>(
            tag: "Shift Templates",
            name: "GetShiftTemplateByManager",
            summary: "Get all shift templates for a specific manager",
            requiresAuth: false,
            canReturnNotFound: false);
    }
}
