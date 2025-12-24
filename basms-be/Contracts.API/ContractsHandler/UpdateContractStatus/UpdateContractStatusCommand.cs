namespace Contracts.API.ContractsHandler.UpdateContractStatus;

public record UpdateContractStatusCommand(
    Guid ContractId,
    string NewStatus = "shift_generated",
    Guid? UpdatedBy = null
) : ICommand<UpdateContractStatusResult>;

public record UpdateContractStatusResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? ContractId { get; init; }
    public string? ContractNumber { get; init; }
    public string? OldStatus { get; init; }
    public string? NewStatus { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
