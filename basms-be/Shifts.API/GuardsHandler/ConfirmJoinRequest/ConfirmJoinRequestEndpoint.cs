namespace Shifts.API.GuardsHandler.ConfirmJoinRequest;

public class ConfirmJoinRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/shifts/guards/{guardId:guid}/confirm-join", async (Guid guardId, ISender sender) =>
        {
            var command = new ConfirmJoinRequestCommand(guardId);
            var result = await sender.Send(command);
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
        .WithSummary("Confirm guard join request");
    }
}
