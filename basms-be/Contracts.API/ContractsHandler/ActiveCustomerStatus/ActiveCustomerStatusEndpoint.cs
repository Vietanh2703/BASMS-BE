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
        .WithSummary("Activate customer status");
    }
}
