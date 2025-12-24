namespace Contracts.API.ContractsHandler.GetAllContractsByCustomerId;

public record GetAllContractsByCustomerIdQuery(Guid CustomerId) : IQuery<GetAllContractsByCustomerIdResult>;

public record ContractDetailDto
{
    public Guid Id { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? DocumentId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractTitle { get; init; } = string.Empty;
    public string ContractType { get; init; } = string.Empty;
    public string ServiceScope { get; init; } = string.Empty;

    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int DurationMonths { get; init; }

    public bool IsRenewable { get; init; }
    public bool AutoRenewal { get; init; }
    public int RenewalNoticeDays { get; init; }
    public int RenewalCount { get; init; }

    public string CoverageModel { get; init; } = string.Empty;

    public bool FollowsCustomerCalendar { get; init; }
    public bool WorkOnPublicHolidays { get; init; }
    public bool WorkOnCustomerClosedDays { get; init; }
    public bool AutoGenerateShifts { get; init; }
    public int GenerateShiftsAdvanceDays { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? ApprovedBy { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime? ActivatedAt { get; init; }
    public DateTime? TerminationDate { get; init; }
    public string? TerminationType { get; init; }
    public string? TerminationReason { get; init; }
    public Guid? TerminatedBy { get; init; }
    public string? ContractFileUrl { get; init; }
    public DateTime? SignedDate { get; init; }
    public string? Notes { get; init; }
    public decimal? MonthlyWage { get; init; }
    public string? MonthlyWageInWords { get; init; }
    public string? CertificationLevel { get; init; }
    public string? JobTitle { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? UpdatedBy { get; init; }
}


public record GetAllContractsByCustomerIdResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerCode { get; init; }
    public string? CustomerName { get; init; }
    public int TotalContracts { get; init; }
    public List<ContractDetailDto> Contracts { get; init; } = new();
}


internal class GetAllContractsByCustomerIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetAllContractsByCustomerIdHandler> logger)
    : IQueryHandler<GetAllContractsByCustomerIdQuery, GetAllContractsByCustomerIdResult>
{
    public async Task<GetAllContractsByCustomerIdResult> Handle(
        GetAllContractsByCustomerIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting all contracts for customer: {CustomerId}", request.CustomerId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            var customerQuery = @"
                SELECT
                    CustomerCode,
                    COALESCE(CompanyName, ContactPersonName) AS CustomerName
                FROM customers
                WHERE Id = @CustomerId AND IsDeleted = 0
            ";

            var customer = await connection.QuerySingleOrDefaultAsync<(string CustomerCode, string CustomerName)>(
                customerQuery,
                new { request.CustomerId });

            if (customer.CustomerCode == null)
            {
                logger.LogWarning("Customer not found: {CustomerId}", request.CustomerId);
                return new GetAllContractsByCustomerIdResult
                {
                    Success = false,
                    ErrorMessage = $"Customer with ID {request.CustomerId} not found"
                };
            }

            var contractsQuery = @"
                SELECT
                    Id,
                    CustomerId,
                    DocumentId,
                    ContractNumber,
                    ContractTitle,
                    ContractType,
                    ServiceScope,
                    StartDate,
                    EndDate,
                    DurationMonths,
                    IsRenewable,
                    AutoRenewal,
                    RenewalNoticeDays,
                    RenewalCount,
                    CoverageModel,
                    FollowsCustomerCalendar,
                    WorkOnPublicHolidays,
                    WorkOnCustomerClosedDays,
                    AutoGenerateShifts,
                    GenerateShiftsAdvanceDays,
                    Status,
                    ApprovedBy,
                    ApprovedAt,
                    ActivatedAt,
                    TerminationDate,
                    TerminationType,
                    TerminationReason,
                    TerminatedBy,
                    ContractFileUrl,
                    SignedDate,
                    Notes,
                    MonthlyWage,
                    MonthlyWageInWords,
                    CertificationLevel,
                    JobTitle,
                    CreatedAt,
                    UpdatedAt,
                    CreatedBy,
                    UpdatedBy
                FROM contracts
                WHERE CustomerId = @CustomerId AND IsDeleted = 0
                ORDER BY StartDate DESC, CreatedAt DESC
            ";

            var contracts = await connection.QueryAsync<ContractDetailDto>(
                contractsQuery,
                new { request.CustomerId });

            var contractsList = contracts.ToList();

            logger.LogInformation(
                "Successfully retrieved {Count} contract(s) for customer {CustomerCode} ({CustomerName})",
                contractsList.Count, customer.CustomerCode, customer.CustomerName);

            return new GetAllContractsByCustomerIdResult
            {
                Success = true,
                CustomerId = request.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                TotalContracts = contractsList.Count,
                Contracts = contractsList
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting contracts for customer: {CustomerId}", request.CustomerId);
            return new GetAllContractsByCustomerIdResult
            {
                Success = false,
                ErrorMessage = $"Error getting contracts: {ex.Message}"
            };
        }
    }
}