namespace Contracts.API.ContractsHandler.GetContractIdByCustomer;

// ================================================================
// QUERY & RESULT
// ================================================================

/// <summary>
/// Query để lấy danh sách contract IDs theo customer ID
/// </summary>
public record GetContractIdByCustomerQuery(Guid CustomerId) : IQuery<GetContractIdByCustomerResult>;

/// <summary>
/// DTO cho Contract ID info
/// </summary>
public record ContractIdDto
{
    public Guid Id { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractTitle { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
}

/// <summary>
/// Kết quả query
/// </summary>
public record GetContractIdByCustomerResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerCode { get; init; }
    public List<ContractIdDto> Contracts { get; init; } = new();
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// Handler để lấy danh sách contract IDs theo customer ID
/// </summary>
internal class GetContractIdByCustomerHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetContractIdByCustomerHandler> logger)
    : IQueryHandler<GetContractIdByCustomerQuery, GetContractIdByCustomerResult>
{
    public async Task<GetContractIdByCustomerResult> Handle(
        GetContractIdByCustomerQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting contract IDs for customer: {CustomerId}", request.CustomerId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // 1. CHECK IF CUSTOMER EXISTS
            // ================================================================
            var customerQuery = @"
                SELECT CustomerCode
                FROM customers
                WHERE Id = @CustomerId AND IsDeleted = 0
            ";

            var customerCode = await connection.QuerySingleOrDefaultAsync<string>(
                customerQuery,
                new { CustomerId = request.CustomerId });

            if (customerCode == null)
            {
                logger.LogWarning("Customer not found: {CustomerId}", request.CustomerId);
                return new GetContractIdByCustomerResult
                {
                    Success = false,
                    ErrorMessage = $"Customer with ID {request.CustomerId} not found"
                };
            }

            // ================================================================
            // 2. GET CONTRACT IDs
            // ================================================================
            var contractsQuery = @"
                SELECT
                    Id,
                    ContractNumber,
                    ContractTitle,
                    Status,
                    StartDate,
                    EndDate
                FROM contracts
                WHERE CustomerId = @CustomerId AND IsDeleted = 0
                ORDER BY StartDate DESC
            ";

            var contracts = await connection.QueryAsync<ContractIdDto>(
                contractsQuery,
                new { CustomerId = request.CustomerId });

            var contractsList = contracts.ToList();

            logger.LogInformation(
                "Successfully retrieved {Count} contract(s) for customer {CustomerCode}",
                contractsList.Count, customerCode);

            return new GetContractIdByCustomerResult
            {
                Success = true,
                CustomerId = request.CustomerId,
                CustomerCode = customerCode,
                Contracts = contractsList
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting contract IDs for customer: {CustomerId}", request.CustomerId);
            return new GetContractIdByCustomerResult
            {
                Success = false,
                ErrorMessage = $"Error getting contract IDs: {ex.Message}"
            };
        }
    }
}
