using BuildingBlocks.Messaging.Events;
using Dapper;

namespace Contracts.API.Consumers;

/// <summary>
/// Consumer trả về thông tin customer từ contractId
/// Used by: Shifts.API để gửi email thông báo hủy ca cho customer
/// </summary>
public class GetCustomerByContractConsumer : IConsumer<GetCustomerByContractRequest>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<GetCustomerByContractConsumer> _logger;

    public GetCustomerByContractConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<GetCustomerByContractConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetCustomerByContractRequest> context)
    {
        var request = context.Message;

        _logger.LogInformation(
            "Received GetCustomerByContractRequest for ContractId: {ContractId}",
            request.ContractId);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            // Query contract and join with customers table to get customer info
            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT
                    c.Id AS ContractId,
                    c.CustomerId,
                    cust.CompanyName,
                    cust.Email,
                    cust.ContactPersonName
                FROM contracts c
                INNER JOIN customers cust ON c.CustomerId = cust.Id
                WHERE c.Id = @ContractId
                  AND c.IsDeleted = 0
                  AND cust.IsDeleted = 0",
                new { request.ContractId });

            if (result == null)
            {
                _logger.LogWarning(
                    "Contract {ContractId} or associated customer not found",
                    request.ContractId);

                // Return error response
                await context.RespondAsync(new GetCustomerByContractResponse
                {
                    Success = false,
                    Customer = null,
                    ErrorMessage = "Contract hoặc Customer không tồn tại"
                });
                return;
            }

            var response = new GetCustomerByContractResponse
            {
                Success = true,
                Customer = new CustomerData
                {
                    CustomerId = (Guid)result.CustomerId,
                    CompanyName = result.CompanyName ?? string.Empty,
                    Email = result.Email ?? string.Empty,
                    ContactPersonName = result.ContactPersonName ?? string.Empty
                },
                ErrorMessage = null
            };

            _logger.LogInformation(
                "Returning customer info for {CompanyName} (Email: {Email})",
                response.Customer.CompanyName,
                response.Customer.Email);

            await context.RespondAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get customer info for ContractId: {ContractId}",
                request.ContractId);

            throw; // Re-throw to trigger MassTransit retry
        }
    }
}
