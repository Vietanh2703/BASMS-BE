namespace Contracts.API.ContractsHandler.ActivateContract;


public record ActivateContractCommand(
    Guid ContractId,
    Guid? ActivatedBy = null,
    Guid? ManagerId = null,
    string? Notes = null
) : ICommand<ActivateContractResult>;

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
