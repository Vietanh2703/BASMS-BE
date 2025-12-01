namespace Contracts.API.ContractsHandler.UpdateCustomer;

// ================================================================
// COMMAND & RESULT
// ================================================================

/// <summary>
/// Command để update thông tin customer
/// </summary>
public record UpdateCustomerCommand : ICommand<UpdateCustomerResult>
{
    public Guid CustomerId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string ContactPersonName { get; init; } = string.Empty;
    public string? ContactPersonTitle { get; init; }
    public string IdentityNumber { get; init; } = string.Empty;
    public DateTime? IdentityIssueDate { get; init; }
    public string? IdentityIssuePlace { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string? Gender { get; init; }
    public DateTime DateOfBirth { get; init; }
    public string Address { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? District { get; init; }
    public string? Industry { get; init; }
    public string? CompanySize { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool FollowsNationalHolidays { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Kết quả update customer
/// </summary>
public record UpdateCustomerResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerCode { get; init; }
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// Handler để update thông tin customer
/// </summary>
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

            // ================================================================
            // 1. CHECK IF CUSTOMER EXISTS
            // ================================================================
            var checkQuery = @"
                SELECT CustomerCode
                FROM customers
                WHERE Id = @CustomerId AND IsDeleted = 0
            ";

            var customerCode = await connection.QuerySingleOrDefaultAsync<string>(
                checkQuery,
                new { CustomerId = request.CustomerId });

            if (customerCode == null)
            {
                logger.LogWarning("Customer not found: {CustomerId}", request.CustomerId);
                return new UpdateCustomerResult
                {
                    Success = false,
                    ErrorMessage = $"Customer with ID {request.CustomerId} not found"
                };
            }

            // ================================================================
            // 2. CHECK EMAIL UNIQUENESS (if changed)
            // ================================================================
            var emailCheckQuery = @"
                SELECT COUNT(*)
                FROM customers
                WHERE Email = @Email
                AND Id != @CustomerId
                AND IsDeleted = 0
            ";

            var emailExists = await connection.ExecuteScalarAsync<int>(
                emailCheckQuery,
                new { Email = request.Email, CustomerId = request.CustomerId });

            if (emailExists > 0)
            {
                logger.LogWarning("Email already exists: {Email}", request.Email);
                return new UpdateCustomerResult
                {
                    Success = false,
                    ErrorMessage = $"Email {request.Email} is already in use by another customer"
                };
            }

            // ================================================================
            // 3. CHECK IDENTITY NUMBER UNIQUENESS (if changed)
            // ================================================================
            var identityCheckQuery = @"
                SELECT COUNT(*)
                FROM customers
                WHERE IdentityNumber = @IdentityNumber
                AND Id != @CustomerId
                AND IsDeleted = 0
            ";

            var identityExists = await connection.ExecuteScalarAsync<int>(
                identityCheckQuery,
                new { IdentityNumber = request.IdentityNumber, CustomerId = request.CustomerId });

            if (identityExists > 0)
            {
                logger.LogWarning("Identity number already exists: {IdentityNumber}", request.IdentityNumber);
                return new UpdateCustomerResult
                {
                    Success = false,
                    ErrorMessage = $"Identity number {request.IdentityNumber} is already in use by another customer"
                };
            }

            // ================================================================
            // 4. UPDATE CUSTOMER
            // ================================================================
            var updateQuery = @"
                UPDATE customers SET
                    CompanyName = @CompanyName,
                    ContactPersonName = @ContactPersonName,
                    ContactPersonTitle = @ContactPersonTitle,
                    IdentityNumber = @IdentityNumber,
                    IdentityIssueDate = @IdentityIssueDate,
                    IdentityIssuePlace = @IdentityIssuePlace,
                    Email = @Email,
                    Phone = @Phone,
                    AvatarUrl = @AvatarUrl,
                    Gender = @Gender,
                    DateOfBirth = @DateOfBirth,
                    Address = @Address,
                    City = @City,
                    District = @District,
                    Industry = @Industry,
                    CompanySize = @CompanySize,
                    Status = @Status,
                    FollowsNationalHolidays = @FollowsNationalHolidays,
                    Notes = @Notes,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @CustomerId AND IsDeleted = 0
            ";

            var rowsAffected = await connection.ExecuteAsync(updateQuery, new
            {
                CustomerId = request.CustomerId,
                CompanyName = request.CompanyName,
                ContactPersonName = request.ContactPersonName,
                ContactPersonTitle = request.ContactPersonTitle,
                IdentityNumber = request.IdentityNumber,
                IdentityIssueDate = request.IdentityIssueDate,
                IdentityIssuePlace = request.IdentityIssuePlace,
                Email = request.Email,
                Phone = request.Phone,
                AvatarUrl = request.AvatarUrl,
                Gender = request.Gender,
                DateOfBirth = request.DateOfBirth,
                Address = request.Address,
                City = request.City,
                District = request.District,
                Industry = request.Industry,
                CompanySize = request.CompanySize,
                Status = request.Status,
                FollowsNationalHolidays = request.FollowsNationalHolidays,
                Notes = request.Notes,
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
