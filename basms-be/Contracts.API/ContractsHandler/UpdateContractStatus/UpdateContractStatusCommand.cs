using BuildingBlocks.CQRS;

namespace Contracts.API.ContractsHandler.UpdateContractStatus;

/// <summary>
/// Command để update contract status thành shift_generated
/// Được gọi từ Shifts.API sau khi generate shifts thành công
/// </summary>
public record UpdateContractStatusCommand(
    Guid ContractId,
    string NewStatus = "shift_generated",
    Guid? UpdatedBy = null
) : ICommand<UpdateContractStatusResult>;

/// <summary>
/// Result sau khi update contract status
/// </summary>
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
