namespace Contracts.API.ContractsHandler.ActiveCustomerStatus;

public class ActiveCustomerStatusEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/customers/{customerId:guid}/activate", async (Guid customerId, ISender sender) =>
        {
            var command = new ActiveCustomerStatusCommand(customerId);
            var result = await sender.Send(command);

            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = result.ErrorMessage,
                    customerId = result.CustomerId,
                    currentStatus = result.OldStatus
                });
            }

            return Results.Ok(new
            {
                success = true,
                message = "Customer status activated successfully",
                customerId = result.CustomerId,
                oldStatus = result.OldStatus,
                newStatus = result.NewStatus,
                updatedAt = result.UpdatedAt
            });
        })
        .RequireAuthorization()
        .WithTags("Contracts")
        .WithName("ActiveCustomerStatus")
        .Produces<ActiveCustomerStatusResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Activate customer status")
        .WithDescription(@"
            Updates customer status from 'schedule_shifts' to 'active'.

            **Use Case:**
            This endpoint is used after shifts have been scheduled for a customer.
            It transitions the customer from the 'schedule_shifts' state (waiting for
            shift scheduling) to 'active' state (shifts are scheduled and ready).

            **Validation:**
            - Customer must exist and not be deleted
            - Customer status must be exactly 'schedule_shifts'
            - If status is different, the update will be rejected

            **Response Structure:**
            ```json
            {
              ""success"": true,
              ""message"": ""Customer status activated successfully"",
              ""customerId"": ""660e8400-e29b-41d4-a716-446655440000"",
              ""oldStatus"": ""schedule_shifts"",
              ""newStatus"": ""active"",
              ""updatedAt"": ""2025-01-15T10:30:00Z""
            }
            ```
        ");
    }
}
