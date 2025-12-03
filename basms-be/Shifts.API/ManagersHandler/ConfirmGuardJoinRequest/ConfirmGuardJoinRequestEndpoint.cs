// Endpoint API để manager xác nhận hoặc từ chối guard join request
namespace Shifts.API.ManagersHandler.ConfirmGuardJoinRequest;

/// <summary>
/// Request DTO từ client
/// </summary>
public record ConfirmGuardJoinRequestRequest(
    Guid GuardId,
    bool IsApproved,
    string? ResponseNote
);

public class ConfirmGuardJoinRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/managers/{managerId}/confirm-guard-join-request", async (
            Guid managerId,
            ConfirmGuardJoinRequestRequest request,
            ISender sender) =>
        {
            var command = new ConfirmGuardJoinRequestCommand(
                managerId,
                request.GuardId,
                request.IsApproved,
                request.ResponseNote
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
                    isApproved = result.IsApproved,
                    processedAt = result.ProcessedAt
                });
            }

            return Results.Ok(new
            {
                success = true,
                message = result.Message,
                guardId = result.GuardId,
                managerId = result.ManagerId,
                isApproved = result.IsApproved,
                newContractType = result.NewContractType,
                processedAt = result.ProcessedAt
            });
        })
        .RequireAuthorization()
        .WithTags("Managers")
        .WithName("ConfirmGuardJoinRequest")
        .Produces<ConfirmGuardJoinRequestResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Approve or reject guard join request")
        .WithDescription(@"
            Manager approves or rejects a guard's join request.

            **Process:**

            **If APPROVED (isApproved = true):**
            1. Updates guard's ContractType: 'join_in_request' → 'accepted_request'
            2. Guard remains assigned to this manager (DirectManagerId unchanged)
            3. Manager's TotalGuardsSupervised count is updated
            4. Guard can now be assigned to teams and shifts

            **If REJECTED (isApproved = false):**
            1. Resets guard's DirectManagerId to NULL
            2. Resets guard's ContractType to NULL
            3. Guard can send new requests to other managers

            **Request Body:**
            ```json
            {
              ""guardId"": ""550e8400-e29b-41d4-a716-446655440000"",
              ""isApproved"": true,
              ""responseNote"": ""Welcome to the team!""
            }
            ```

            **Response (Approved):**
            ```json
            {
              ""success"": true,
              ""message"": ""Successfully approved Trần Văn B's join request. They are now part of your team."",
              ""guardId"": ""550e8400-e29b-41d4-a716-446655440000"",
              ""managerId"": ""660e8400-e29b-41d4-a716-446655440000"",
              ""isApproved"": true,
              ""newContractType"": ""accepted_request"",
              ""processedAt"": ""2024-01-15T10:30:00Z""
            }
            ```

            **Response (Rejected):**
            ```json
            {
              ""success"": true,
              ""message"": ""Join request from Trần Văn B has been rejected."",
              ""guardId"": ""550e8400-e29b-41d4-a716-446655440000"",
              ""managerId"": ""660e8400-e29b-41d4-a716-446655440000"",
              ""isApproved"": false,
              ""newContractType"": ""rejected"",
              ""processedAt"": ""2024-01-15T10:30:00Z""
            }
            ```
        ");
    }
}
