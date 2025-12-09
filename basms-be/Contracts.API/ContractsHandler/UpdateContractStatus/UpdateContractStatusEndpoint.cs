using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Contracts.API.ContractsHandler.UpdateContractStatus;

/// <summary>
/// Endpoint để update contract status
/// Được gọi từ Shifts.API sau khi generate shifts thành công
/// </summary>
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
                "✓ Contract {ContractNumber} status updated: {OldStatus} → {NewStatus}",
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
        .WithDescription(@"
Update contract status (thường được gọi từ Shifts.API sau khi generate shifts thành công)

## Request Body:
```json
{
  ""newStatus"": ""shift_generated"",
  ""updatedBy"": ""guid"" // optional
}
```

## Response:
```json
{
  ""success"": true,
  ""contractId"": ""guid"",
  ""contractNumber"": ""CTR-2025-001"",
  ""oldStatus"": ""schedule_shifts"",
  ""newStatus"": ""shift_generated"",
  ""updatedAt"": ""2025-12-09T10:30:00Z"",
  ""message"": ""Contract CTR-2025-001 status updated from schedule_shifts to shift_generated""
}
```
")
        .Produces(200)
        .Produces(400)
        .Produces(404);
    }
}

/// <summary>
/// Request DTO cho update contract status
/// </summary>
public record UpdateContractStatusRequest
{
    /// <summary>
    /// Status mới (mặc định: shift_generated)
    /// </summary>
    public string NewStatus { get; init; } = "shift_generated";

    /// <summary>
    /// ID của user update
    /// </summary>
    public Guid? UpdatedBy { get; init; }
}
