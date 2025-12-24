namespace Contracts.API.ContractsHandler.UpdateCustomer;

public record UpdateCustomerCommand : ICommand<UpdateCustomerResult>
{
    public Guid CustomerId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string ContactPersonName { get; init; } = string.Empty;
    public string? ContactPersonTitle { get; init; }
    public string IdentityNumber { get; init; } = string.Empty;
    public DateTime? IdentityIssueDate { get; init; }
    public string? IdentityIssuePlace { get; init; }
    public DateTime DateOfBirth { get; init; }
    public string Address { get; init; } = string.Empty;
}

public record UpdateCustomerResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerCode { get; init; }
}

internal class UpdateCustomerHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<UpdateCustomerHandler> logger)
    : ICommandHandler<UpdateCustomerCommand, UpdateCustomerResult>
{
    public async Task<UpdateCustomerResult> Handle(
        UpdateCustomerCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Updating customer: {CustomerId}", request.CustomerId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            var checkQuery = @"
                SELECT CustomerCode
                FROM customers
                WHERE Id = @CustomerId AND IsDeleted = 0
            ";

            var customerCode = await connection.QuerySingleOrDefaultAsync<string>(
                checkQuery,
                new { request.CustomerId });

            if (customerCode == null)
            {
                logger.LogWarning("Customer not found: {CustomerId}", request.CustomerId);
                return new UpdateCustomerResult
                {
                    Success = false,
                    ErrorMessage = $"Customer with ID {request.CustomerId} not found"
                };
            }

            var identityCheckQuery = @"
                SELECT COUNT(*)
                FROM customers
                WHERE IdentityNumber = @IdentityNumber
                AND Id != @CustomerId
                AND IsDeleted = 0
            ";

            var identityExists = await connection.ExecuteScalarAsync<int>(
                identityCheckQuery,
                new { request.IdentityNumber, request.CustomerId });

            if (identityExists > 0)
            {
                logger.LogWarning("Identity number already exists: {IdentityNumber}", request.IdentityNumber);
                return new UpdateCustomerResult
                {
                    Success = false,
                    ErrorMessage = $"Identity number {request.IdentityNumber} is already in use by another customer"
                };
            }
            
            var updateQuery = @"
                UPDATE customers SET
                    CompanyName = @CompanyName,
                    ContactPersonName = @ContactPersonName,
                    ContactPersonTitle = @ContactPersonTitle,
                    IdentityNumber = @IdentityNumber,
                    IdentityIssueDate = @IdentityIssueDate,
                    IdentityIssuePlace = @IdentityIssuePlace,
                    DateOfBirth = @DateOfBirth,
                    Address = @Address,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @CustomerId AND IsDeleted = 0
            ";

            var rowsAffected = await connection.ExecuteAsync(updateQuery, new
            {
                request.CustomerId,
                request.CompanyName,
                request.ContactPersonName,
                request.ContactPersonTitle,
                request.IdentityNumber,
                request.IdentityIssueDate,
                request.IdentityIssuePlace,
                request.DateOfBirth,
                request.Address,
                UpdatedAt = DateTime.UtcNow
            });

            if (rowsAffected == 0)
            {
                logger.LogWarning("No rows affected when updating customer: {CustomerId}", request.CustomerId);
                return new UpdateCustomerResult
                {
                    Success = false,
                    ErrorMessage = "Failed to update customer"
                };
            }

            logger.LogInformation(
                "Successfully updated customer {CustomerCode} (ID: {CustomerId})",
                customerCode, request.CustomerId);

            return new UpdateCustomerResult
            {
                Success = true,
                CustomerId = request.CustomerId,
                CustomerCode = customerCode
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating customer: {CustomerId}", request.CustomerId);
            return new UpdateCustomerResult
            {
                Success = false,
                ErrorMessage = $"Error updating customer: {ex.Message}"
            };
        }
    }
}
