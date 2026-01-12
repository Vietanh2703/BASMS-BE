using Shifts.API.Utilities;

namespace Shifts.API.ShiftsHandler.GetContractManaged;

public class GetContractManagedEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/contracts/managed/{managerId:guid}", async (
            [FromRoute] Guid managerId,
            [FromQuery] string? status,
            ISender sender,
            ILogger<GetContractManagedEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            var query = new GetContractManagedQuery(
                ManagerId: managerId,
                Status: status
            );

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning("Failed to get contracts for Manager {ManagerId}: {Error}", managerId, result.ErrorMessage);
                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation("Found {Count} unique contracts for Manager {ManagerId}", result.TotalCount, managerId);

            return Results.Ok(new
            {
                success = true,
                data = result.Contracts,
                totalCount = result.TotalCount,
                filters = new
                {
                    managerId,
                    status = status ?? "all"
                }
            });
        })
        .AddStandardGetDocumentation<object>(
            tag: "Contracts - Manager",
            name: "GetContractManaged",
            summary: "Lấy danh sách contracts UNIQUE mà manager phụ trách",
            canReturnNotFound: false);
    }
}
