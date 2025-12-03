// Handler xử lý logic confirm join request
// Update ContractType từ "join_in_request" sang "joined_in"
namespace Shifts.API.GuardsHandler.ConfirmJoinRequest;

// ================================================================
// COMMAND & RESULT
// ================================================================

/// <summary>
/// Command để confirm join request - update ContractType sang "joined_in"
/// </summary>
public record ConfirmJoinRequestCommand(Guid GuardId) : ICommand<ConfirmJoinRequestResult>;

/// <summary>
/// Result chứa kết quả confirm
/// </summary>
public record ConfirmJoinRequestResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid GuardId { get; init; }
    public string? EmployeeCode { get; init; }
    public string? OldContractType { get; init; }
    public string? NewContractType { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

// ================================================================
// HANDLER
// ================================================================

internal class ConfirmJoinRequestHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ConfirmJoinRequestHandler> logger)
    : ICommandHandler<ConfirmJoinRequestCommand, ConfirmJoinRequestResult>
{
    public async Task<ConfirmJoinRequestResult> Handle(
        ConfirmJoinRequestCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Confirming join request for Guard: {GuardId}",
                request.GuardId);
            using var connection = await connectionFactory.CreateConnectionAsync();
            
            var guards = await connection.GetAllAsync<Guards>();
            var guard = guards.FirstOrDefault(g => g.Id == request.GuardId && !g.IsDeleted);

            if (guard == null)
            {
                logger.LogWarning("Guard not found: {GuardId}", request.GuardId);
                return new ConfirmJoinRequestResult
                {
                    Success = false,
                    ErrorMessage = $"Guard with ID {request.GuardId} not found",
                    GuardId = request.GuardId
                };
            }

            string oldContractType = guard.ContractType ?? string.Empty;
            
            if (oldContractType != "join_in_request")
            {
                logger.LogWarning(
                    "Guard {EmployeeCode} ContractType is '{OldContractType}', not 'join_in_request'. Skipping update.",
                    guard.EmployeeCode,
                    oldContractType);

                return new ConfirmJoinRequestResult
                {
                    Success = false,
                    ErrorMessage = $"Guard ContractType is '{oldContractType}', expected 'join_in_request'",
                    GuardId = request.GuardId,
                    EmployeeCode = guard.EmployeeCode,
                    OldContractType = oldContractType,
                    NewContractType = oldContractType
                };
            }
            guard.ContractType = "joined_in";
            guard.UpdatedAt = DateTime.UtcNow;

            var updated = await connection.UpdateAsync(guard);

            if (!updated)
            {
                logger.LogError(
                    "Failed to update guard {EmployeeCode} ContractType",
                    guard.EmployeeCode);

                return new ConfirmJoinRequestResult
                {
                    Success = false,
                    ErrorMessage = "Failed to update guard ContractType",
                    GuardId = request.GuardId,
                    EmployeeCode = guard.EmployeeCode,
                    OldContractType = oldContractType
                };
            }

            logger.LogInformation(
                "Successfully confirmed join request for guard {EmployeeCode} (ID: {GuardId}) from 'join_in_request' to 'joined_in'",
                guard.EmployeeCode,
                request.GuardId);

            return new ConfirmJoinRequestResult
            {
                Success = true,
                GuardId = request.GuardId,
                EmployeeCode = guard.EmployeeCode,
                OldContractType = oldContractType,
                NewContractType = "joined_in",
                UpdatedAt = guard.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error confirming join request for GuardId: {GuardId}", request.GuardId);
            return new ConfirmJoinRequestResult
            {
                Success = false,
                ErrorMessage = $"Error confirming join request: {ex.Message}",
                GuardId = request.GuardId
            };
        }
    }
}
