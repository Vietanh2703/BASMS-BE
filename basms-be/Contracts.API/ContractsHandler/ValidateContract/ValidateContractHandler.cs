namespace Contracts.API.ContractsHandler.ValidateContract;

// Query để validate contract
public record ValidateContractQuery(Guid ContractId) : IQuery<ValidateContractResult>;

// Result chứa thông tin validation
public record ValidateContractResult(
    bool IsValid,
    bool Exists,
    bool IsActive,
    string? ErrorMessage,
    ContractDto? Contract
);

// DTO chứa thông tin contract
public record ContractDto(
    Guid Id,
    string ContractNumber,
    string ContractTitle,
    Guid CustomerId,
    string CustomerName,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    bool WorkOnPublicHolidays,
    bool WorkOnCustomerClosedDays,
    bool AutoGenerateShifts
);

internal class ValidateContractHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ValidateContractHandler> logger)
    : IQueryHandler<ValidateContractQuery, ValidateContractResult>
{
    public async Task<ValidateContractResult> Handle(
        ValidateContractQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Validating contract {ContractId}", request.ContractId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // Lấy contract
            var contract = await connection.GetAsync<Contract>(request.ContractId);

            if (contract == null || contract.IsDeleted)
            {
                return new ValidateContractResult(
                    IsValid: false,
                    Exists: false,
                    IsActive: false,
                    ErrorMessage: "Contract not found",
                    Contract: null
                );
            }

            // Check contract active
            bool isActive = contract.Status.ToLower() == "active" &&
                           contract.StartDate <= DateTime.UtcNow &&
                           contract.EndDate >= DateTime.UtcNow;

            if (!isActive)
            {
                return new ValidateContractResult(
                    IsValid: false,
                    Exists: true,
                    IsActive: false,
                    ErrorMessage: $"Contract status is {contract.Status} or outside date range",
                    Contract: null
                );
            }

            // Lấy customer name
            var customer = await connection.GetAsync<Customer>(contract.CustomerId);
            var customerName = customer?.CompanyName ?? "Unknown";

            // Map to DTO
            var contractDto = new ContractDto(
                Id: contract.Id,
                ContractNumber: contract.ContractNumber,
                ContractTitle: contract.ContractTitle,
                CustomerId: contract.CustomerId,
                CustomerName: customerName,
                StartDate: contract.StartDate,
                EndDate: contract.EndDate,
                Status: contract.Status,
                WorkOnPublicHolidays: contract.WorkOnPublicHolidays,
                WorkOnCustomerClosedDays: contract.WorkOnCustomerClosedDays,
                AutoGenerateShifts: contract.AutoGenerateShifts
            );

            logger.LogInformation("Contract {ContractNumber} is valid", contract.ContractNumber);

            return new ValidateContractResult(
                IsValid: true,
                Exists: true,
                IsActive: true,
                ErrorMessage: null,
                Contract: contractDto
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating contract {ContractId}", request.ContractId);
            throw;
        }
    }
}
