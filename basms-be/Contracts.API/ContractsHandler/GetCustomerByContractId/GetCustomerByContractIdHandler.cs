namespace Contracts.API.ContractsHandler.GetCustomerByContractId;

// ================================================================
// QUERY & RESULT
// ================================================================

/// <summary>
/// Query để lấy CustomerId từ ContractId
/// </summary>
public record GetCustomerByContractIdQuery(Guid ContractId) : IQuery<GetCustomerByContractIdResult>;

/// <summary>
/// Kết quả query
/// </summary>
public record GetCustomerByContractIdResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid ContractId { get; init; }
    public string? ContractNumber { get; init; }
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// Handler để lấy CustomerId từ ContractId
/// </summary>
internal class 
    GetCustomerByContractIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetCustomerByContractIdHandler> logger)
    : IQueryHandler<GetCustomerByContractIdQuery, GetCustomerByContractIdResult>
{
    public async Task<GetCustomerByContractIdResult> Handle(
        GetCustomerByContractIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting CustomerId for ContractId: {ContractId}", request.ContractId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // Query contract to get CustomerId
            var query = @"
                SELECT
                    Id,
                    CustomerId,
                    ContractNumber
                FROM contracts
                WHERE Id = @ContractId AND IsDeleted = 0
            ";

            var contract = await connection.QuerySingleOrDefaultAsync<Models.Contract>(
                query,
                new { ContractId = request.ContractId });

            if (contract == null)
            {
                logger.LogWarning("Contract not found: {ContractId}", request.ContractId);
                return new GetCustomerByContractIdResult
                {
                    Success = false,
                    ErrorMessage = $"Contract with ID {request.ContractId} not found",
                    ContractId = request.ContractId
                };
            }

            var customerId = contract.CustomerId;

            if (!customerId.HasValue)
            {
                logger.LogWarning("Contract {ContractId} has no CustomerId", request.ContractId);
                return new GetCustomerByContractIdResult
                {
                    Success = false,
                    ErrorMessage = "Contract has no associated Customer",
                    ContractId = request.ContractId,
                    ContractNumber = contract.ContractNumber
                };
            }

            logger.LogInformation(
                "Successfully retrieved CustomerId {CustomerId} for Contract {ContractNumber}",
                customerId.Value,
                contract.ContractNumber);

            return new GetCustomerByContractIdResult
            {
                Success = true,
                CustomerId = customerId.Value,
                ContractId = request.ContractId,
                ContractNumber = contract.ContractNumber
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting CustomerId for ContractId: {ContractId}", request.ContractId);
            return new GetCustomerByContractIdResult
            {
                Success = false,
                ErrorMessage = $"Error getting CustomerId: {ex.Message}",
                ContractId = request.ContractId
            };
        }
    }
}
