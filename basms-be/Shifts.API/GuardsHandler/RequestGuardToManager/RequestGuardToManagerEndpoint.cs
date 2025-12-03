namespace Shifts.API.GuardsHandler.RequestGuardToManager;

/// <summary>
/// Request DTO từ client
/// </summary>
public record RequestGuardToManagerRequest(
    Guid GuardId,
    Guid ManagerId,
    string? RequestNote
);

public class RequestGuardToManagerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/guards/request-join-manager", async (
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
        .WithSummary("Request to join manager's team")
        .WithDescription(@"
            Guard sends a request to join a manager's team.

            **Process:**
            1. Validates guard and manager existence
            2. Checks for existing pending requests
            3. Updates guard's DirectManagerId
            4. Sets ContractType to 'join_in_request'
            5. Manager can then approve/reject via GetGuardJoinRequest endpoint

            **Request Body:**
            ```json
            {
              ""guardId"": ""550e8400-e29b-41d4-a716-446655440000"",
              ""managerId"": ""660e8400-e29b-41d4-a716-446655440000"",
              ""requestNote"": ""I would like to join your team""
            }
            ```
        ");
    }
}
