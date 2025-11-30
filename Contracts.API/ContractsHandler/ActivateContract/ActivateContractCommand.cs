using BuildingBlocks.CQRS;

namespace Contracts.API.ContractsHandler.ActivateContract;

/// <summary>
/// Command để activate contract (draft → active)
/// Workflow: Manager review contract → Approve → Activate → Publish event to Shifts.API
/// </summary>
public record ActivateContractCommand(
    Guid ContractId,
    Guid? ActivatedBy = null,
    string? Notes = null
) : ICommand<ActivateContractResult>;

/// <summary>
/// Result sau khi activate contract
/// </summary>
public record ActivateContractResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? ContractId { get; init; }
    public string? ContractNumber { get; init; }
    public string? Status { get; init; }
    public DateTime? ActivatedAt { get; init; }
    public ContractActivationInfo? ActivationInfo { get; init; }
}

/// <summary>
/// Thông tin activation để log và tracking
/// </summary>
public record ContractActivationInfo
{
    public int LocationsCount { get; init; }
    public int ShiftSchedulesCount { get; init; }
    public bool AutoGenerateShifts { get; init; }
    public int GenerateShiftsAdvanceDays { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string? CustomerName { get; init; }
    public bool EventPublished { get; init; }
}
