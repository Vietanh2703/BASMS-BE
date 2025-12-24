namespace Contracts.API.ContractsHandler.UpdateContractStatus;

public class UpdateContractStatusHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<UpdateContractStatusHandler> logger)
    : ICommandHandler<UpdateContractStatusCommand, UpdateContractStatusResult>
{
    public async Task<UpdateContractStatusResult> Handle(
        UpdateContractStatusCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Updating contract status - ContractId: {ContractId}, NewStatus: {NewStatus}",
            request.ContractId,
            request.NewStatus);

        using var connection = await connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            var contract = await connection.QueryFirstOrDefaultAsync<Contract>(
                "SELECT * FROM contracts WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.ContractId },
                transaction);

            if (contract == null)
            {
                logger.LogWarning("Contract not found: {ContractId}", request.ContractId);
                return new UpdateContractStatusResult
                {
                    Success = false,
                    ErrorMessage = $"Contract with ID {request.ContractId} not found"
                };
            }

            var oldStatus = contract.Status;

            logger.LogInformation(
                "Contract {ContractNumber} found - Current status: {OldStatus} → New status: {NewStatus}",
                contract.ContractNumber,
                oldStatus,
                request.NewStatus);

            contract.Status = request.NewStatus;
            contract.UpdatedAt = DateTime.UtcNow;
            contract.UpdatedBy = request.UpdatedBy;

            await connection.UpdateAsync(contract, transaction);

            logger.LogInformation(
                "✓ Contract {ContractNumber} status updated: {OldStatus} → {NewStatus}",
                contract.ContractNumber,
                oldStatus,
                contract.Status);

            transaction.Commit();

            return new UpdateContractStatusResult
            {
                Success = true,
                ContractId = contract.Id,
                ContractNumber = contract.ContractNumber,
                OldStatus = oldStatus,
                NewStatus = contract.Status,
                UpdatedAt = contract.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            logger.LogError(ex,
                "Failed to update contract status - ContractId: {ContractId}",
                request.ContractId);

            return new UpdateContractStatusResult
            {
                Success = false,
                ErrorMessage = $"Update failed: {ex.Message}"
            };
        }
    }
}
