using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Shifts.API.ShiftsHandler.GetShiftTemplateByManager;

/// <summary>
/// Endpoint để lấy danh sách shift templates theo manager
/// </summary>
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
            logger.LogInformation(
                "GET /api/shifts/templates/by-manager/{ManagerId} - Getting shift templates",
                managerId);

            var query = new GetShiftTemplateByManagerQuery(
                ManagerId: managerId,
                Status: status,
                IsActive: isActive
            );

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to get shift templates for Manager {ManagerId}: {Error}",
                    managerId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "✓ Found {Count} shift templates for Manager {ManagerId}",
                result.TotalCount,
                managerId);

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
        // .RequireAuthorization()
        .WithName("GetShiftTemplateByManager")
        .WithTags("Shift Templates")
        .Produces(200)
        .Produces(400)
        .WithSummary("Get all shift templates for a specific manager")
        .WithDescription(@"
            Returns all shift templates created/managed by a specific manager.

            Query Parameters:
            - status (optional): Filter by template status (e.g., 'await_create_shift', 'active', 'inactive', 'archived')
            - isActive (optional): Filter by active status (true/false)

            Example:
            GET /api/shifts/templates/by-manager/{managerId}
            GET /api/shifts/templates/by-manager/{managerId}?status=await_create_shift
            GET /api/shifts/templates/by-manager/{managerId}?isActive=true
            GET /api/shifts/templates/by-manager/{managerId}?status=await_create_shift&isActive=true
        ");
    }
}
