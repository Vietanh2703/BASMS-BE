// Endpoint API để confirm join request
// Update ContractType từ "join_in_request" sang "joined_in"
namespace Shifts.API.GuardsHandler.ConfirmJoinRequest;

public class ConfirmJoinRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /api/shifts/guards/{guardId}/confirm-join
        app.MapPost("/api/shifts/guards/{guardId:guid}/confirm-join", async (Guid guardId, ISender sender) =>
        {
            // Bước 1: Tạo command với GuardId
            var command = new ConfirmJoinRequestCommand(guardId);

            // Bước 2: Gửi command đến Handler
            var result = await sender.Send(command);

            // Bước 3: Trả về response
            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = result.ErrorMessage,
                    guardId = result.GuardId,
                    employeeCode = result.EmployeeCode,
                    currentContractType = result.OldContractType
                });
            }

            return Results.Ok(new
            {
                success = true,
                message = "Join request confirmed successfully",
                guardId = result.GuardId,
                employeeCode = result.EmployeeCode,
                oldContractType = result.OldContractType,
                newContractType = result.NewContractType,
                updatedAt = result.UpdatedAt
            });
        })
        .RequireAuthorization()
        .WithTags("Guards")
        .WithName("ConfirmJoinRequest")
        .Produces<ConfirmJoinRequestResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Confirm guard join request")
        .WithDescription(@"
            Confirms a guard's join request by updating their ContractType
            from 'join_in_request' to 'joined_in'.

            **Features:**
            - Updates guard's ContractType
            - Validates current ContractType is 'join_in_request'
            - Sets UpdatedAt timestamp
            - Returns detailed update information

            **Use Case:**
            This endpoint is used when a manager approves a guard's join request.
            It transitions the guard from pending join status to officially joined.

            **Validation:**
            - Guard must exist and not be deleted
            - Guard's ContractType must be exactly 'join_in_request'
            - If ContractType is different, the update will be rejected

            **Workflow:**
            1. Guard sends join request (ContractType = 'join_in_request')
            2. Manager reviews the request
            3. Manager calls this endpoint to confirm
            4. Guard's ContractType becomes 'joined_in'
            5. Guard can now be included in shift assignments

            **Response Structure:**
            ```json
            {
              ""success"": true,
              ""message"": ""Join request confirmed successfully"",
              ""guardId"": ""770e8400-e29b-41d4-a716-446655440000"",
              ""employeeCode"": ""GRD001"",
              ""oldContractType"": ""join_in_request"",
              ""newContractType"": ""joined_in"",
              ""updatedAt"": ""2025-01-15T10:30:00Z""
            }
            ```

            **Error Response (Invalid State):**
            ```json
            {
              ""success"": false,
              ""message"": ""Guard ContractType is 'FULL_TIME', expected 'join_in_request'"",
              ""guardId"": ""770e8400-e29b-41d4-a716-446655440000"",
              ""employeeCode"": ""GRD001"",
              ""currentContractType"": ""FULL_TIME""
            }
            ```
        ");
    }
}
