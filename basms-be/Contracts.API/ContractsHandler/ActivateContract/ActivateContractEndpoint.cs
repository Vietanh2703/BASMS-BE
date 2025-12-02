using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Contracts.API.ContractsHandler.ActivateContract;

/// <summary>
/// Endpoint để activate contract
/// Frontend gọi endpoint này để kích hoạt contract sau khi review
/// </summary>
public class ActivateContractEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/{id:guid}/activate", async (
            [FromRoute] Guid id,
            [FromBody] ActivateContractRequest? request,
            ISender sender,
            ILogger<ActivateContractEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "POST /api/contracts/{ContractId}/activate - Activating contract",
                id);

            var command = new ActivateContractCommand(
                ContractId: id,
                ActivatedBy: request?.ActivatedBy,
                ManagerId: request?.ManagerId,
                Notes: request?.Notes
            );

            var result = await sender.Send(command, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to activate contract {ContractId}: {Error}",
                    id,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                @"✓ Contract {ContractNumber} activated successfully!
                  - Status: {Status}
                  - Activated At: {ActivatedAt}
                  - Locations: {Locations}
                  - Schedules: {Schedules}
                  - Auto Generate: {AutoGenerate}
                  - Event Published: {EventPublished}",
                result.ContractNumber,
                result.Status,
                result.ActivatedAt,
                result.ActivationInfo?.LocationsCount,
                result.ActivationInfo?.ShiftSchedulesCount,
                result.ActivationInfo?.AutoGenerateShifts,
                result.ActivationInfo?.EventPublished);

            return Results.Ok(new
            {
                success = true,
                contractId = result.ContractId,
                contractNumber = result.ContractNumber,
                status = result.Status,
                activatedAt = result.ActivatedAt,
                activationInfo = new
                {
                    locationsCount = result.ActivationInfo?.LocationsCount,
                    shiftSchedulesCount = result.ActivationInfo?.ShiftSchedulesCount,
                    autoGenerateShifts = result.ActivationInfo?.AutoGenerateShifts,
                    generateShiftsAdvanceDays = result.ActivationInfo?.GenerateShiftsAdvanceDays,
                    startDate = result.ActivationInfo?.StartDate,
                    endDate = result.ActivationInfo?.EndDate,
                    customerName = result.ActivationInfo?.CustomerName,
                    eventPublished = result.ActivationInfo?.EventPublished
                },
                message = $"Contract {result.ContractNumber} activated successfully. " +
                         $"Shift templates will be imported and shifts will be auto-generated in Shifts.API."
            });
        })
        // .RequireAuthorization()
        .WithName("ActivateContract")
        .WithTags("Contracts")
        .Produces(200)
        .Produces(400)
        .Produces(404);
    }
}

/// <summary>
/// Request DTO cho activate contract
/// </summary>
public record ActivateContractRequest
{
    /// <summary>
    /// ID của user activate (manager)
    /// </summary>
    public Guid? ActivatedBy { get; init; }

    /// <summary>
    /// Manager ID phụ trách contract này (để điền vào ShiftTemplate)
    /// </summary>
    public Guid? ManagerId { get; init; }

    /// <summary>
    /// Ghi chú khi activate
    /// </summary>
    public string? Notes { get; init; }
}
