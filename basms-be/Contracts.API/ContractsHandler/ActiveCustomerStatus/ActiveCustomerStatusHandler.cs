namespace Contracts.API.ContractsHandler.ActiveCustomerStatus;

// ================================================================
// COMMAND & RESULT
// ================================================================

/// <summary>
/// Command để update customer status từ "in_active" sang "active"
/// </summary>
public record ActiveCustomerStatusCommand(Guid CustomerId) : ICommand<ActiveCustomerStatusResult>;

/// <summary>
/// Kết quả command
/// </summary>
public record ActiveCustomerStatusResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid CustomerId { get; init; }
    public string? OldStatus { get; init; }
    public string? NewStatus { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// Handler để update customer status từ "in_active" sang "active"
/// </summary>
internal class ActiveCustomerStatusHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ActiveCustomerStatusHandler> logger)
    : ICommandHandler<ActiveCustomerStatusCommand, ActiveCustomerStatusResult>
{
    public async Task<ActiveCustomerStatusResult> Handle(
        ActiveCustomerStatusCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Activating customer status for CustomerId: {CustomerId}",
                request.CustomerId);

            using var connection = await connectionFactory.CreateConnectionAsync();
            var getCustomerQuery = @"
                SELECT
                    Id,
                    CustomerCode,
                    CompanyName,
                    Status
                FROM customers
                WHERE Id = @CustomerId AND IsDeleted = 0
            ";

            var customer = await connection.QuerySingleOrDefaultAsync<Models.Customer>(
                getCustomerQuery,
                new { CustomerId = request.CustomerId });

            if (customer == null)
            {
                logger.LogWarning("Customer not found: {CustomerId}", request.CustomerId);
                return new ActiveCustomerStatusResult
                {
                    Success = false,
                    ErrorMessage = $"Customer with ID {request.CustomerId} not found",
                    CustomerId = request.CustomerId
                };
            }

            string oldStatus = customer.Status;
            
            if (oldStatus != "in-active")
            {
                logger.LogWarning(
                    "Customer {CustomerCode} status is '{OldStatus}', not 'in-active'. Skipping update.",
                    customer.CustomerCode,
                    oldStatus);

                return new ActiveCustomerStatusResult
                {
                    Success = false,
                    ErrorMessage = $"Customer status is '{oldStatus}', expected 'in-active'",
                    CustomerId = request.CustomerId,
                    OldStatus = oldStatus,
                    NewStatus = oldStatus
                };
            }
            
            var updateQuery = @"
                UPDATE customers
                SET Status = 'active',
                    UpdatedAt = @UpdatedAt
                WHERE Id = @CustomerId
                  AND Status = 'in-active'
                  AND IsDeleted = 0
            ";

            var updatedAt = DateTime.UtcNow;
            var affectedRows = await connection.ExecuteAsync(updateQuery, new
            {
                CustomerId = request.CustomerId,
                UpdatedAt = updatedAt
            });

            if (affectedRows == 0)
            {
                logger.LogWarning(
                    "Failed to update customer {CustomerCode} status. Status may have changed.",
                    customer.CustomerCode);

                return new ActiveCustomerStatusResult
                {
                    Success = false,
                    ErrorMessage = "Failed to update customer status. Status may have changed.",
                    CustomerId = request.CustomerId,
                    OldStatus = oldStatus
                };
            }

            logger.LogInformation(
                "Successfully activated customer {CustomerCode} (ID: {CustomerId}) from 'in-active' to 'active'",
                customer.CustomerCode,
                request.CustomerId);

            return new ActiveCustomerStatusResult
            {
                Success = true,
                CustomerId = request.CustomerId,
                OldStatus = oldStatus,
                NewStatus = "active",
                UpdatedAt = updatedAt
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error activating customer status for CustomerId: {CustomerId}", request.CustomerId);
            return new ActiveCustomerStatusResult
            {
                Success = false,
                ErrorMessage = $"Error activating customer status: {ex.Message}",
                CustomerId = request.CustomerId
            };
        }
    }
}
