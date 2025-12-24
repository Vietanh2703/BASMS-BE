namespace Shifts.API.GuardsHandler.RequestGuardToManager;

public record RequestGuardToManagerRequest(
    Guid GuardId,
    Guid ManagerId,
    string? RequestNote
);

public class RequestGuardToManagerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/guards/request-join-manager", async (
            RequestGuardToManagerRequest request,
            ISender sender) =>
        {
            var command = new RequestGuardToManagerCommand(
                request.GuardId,
                request.ManagerId,
                request.RequestNote
            );

            var result = await sender.Send(command);

            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = result.Message,
                    guardId = result.GuardId,
                    managerId = result.ManagerId,
                    requestedAt = result.RequestedAt
                });
            }

            return Results.Ok(new
            {
                success = true,
                message = result.Message,
                guardId = result.GuardId,
                managerId = result.ManagerId,
                requestedAt = result.RequestedAt
            });
        })
        .RequireAuthorization()
        .WithTags("Guards")
        .WithName("RequestGuardToManager")
        .Produces<RequestGuardToManagerResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Request to join manager's team");
    }
}
