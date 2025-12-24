namespace Contracts.API.ContractsHandler.UpdateContractStatus;

public class UpdateContractStatusEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/{id:guid}/update-status", async (
            [FromRoute] Guid id,
            [FromBody] UpdateContractStatusRequest? request,
            ISender sender,
            ILogger<UpdateContractStatusEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "POST /api/contracts/{ContractId}/update-status - Updating contract status",
                id);

            var command = new UpdateContractStatusCommand(
                ContractId: id,
                NewStatus: request?.NewStatus ?? "shift_generated",
                UpdatedBy: request?.UpdatedBy
            );

            var result = await sender.Send(command, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to update contract status {ContractId}: {Error}",
                    id,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "Contract {ContractNumber} status updated: {OldStatus} → {NewStatus}",
                result.ContractNumber,
                result.OldStatus,
                result.NewStatus);

            return Results.Ok(new
            {
                success = true,
                contractId = result.ContractId,
                contractNumber = result.ContractNumber,
                oldStatus = result.OldStatus,
                newStatus = result.NewStatus,
                updatedAt = result.UpdatedAt,
                message = $"Contract {result.ContractNumber} status updated from {result.OldStatus} to {result.NewStatus}"
            });
        })
            .RequireAuthorization()
        .WithName("UpdateContractStatus")
        .WithTags("Contracts")
        .WithSummary("Update contract status")
        .Produces(200)
        .Produces(400)
        .Produces(404);
    }
}

public record UpdateContractStatusRequest
{
    public string NewStatus { get; init; } = "shift_generated";
    public Guid? UpdatedBy { get; init; }
}
