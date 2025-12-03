using Contracts.API.ContractsHandler.GetCustomerById;

namespace Contracts.API.ContractsHandler.GetCustomerFromEmail;

// ================================================================
// QUERY & RESULT
// ================================================================

/// <summary>
/// Query để lấy customer detail by email
/// </summary>
public record GetCustomerFromEmailQuery(string Email) : IQuery<GetCustomerFromEmailResult>;

/// <summary>
/// Kết quả query
/// </summary>
public record GetCustomerFromEmailResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public CustomerDetailDto? Customer { get; init; }
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// Handler để lấy customer detail theo email
/// </summary>
internal class GetCustomerFromEmailHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetCustomerFromEmailHandler> logger)
    : IQueryHandler<GetCustomerFromEmailQuery, GetCustomerFromEmailResult>
{
    public async Task<GetCustomerFromEmailResult> Handle(
        GetCustomerFromEmailQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting customer by Email: {Email}", request.Email);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // Query customer by email
            var query = @"
                SELECT
                    Id, CustomerCode, CompanyName, ContactPersonName, ContactPersonTitle,
                    IdentityNumber, IdentityIssueDate, IdentityIssuePlace,
                    Email, Phone, AvatarUrl, Gender, DateOfBirth,
                    Address, City, District, Industry, CompanySize,
                    Status, CustomerSince, FollowsNationalHolidays, Notes, CreatedAt
                FROM customers
                WHERE Email = @Email
                  AND IsDeleted = 0
            ";

            var customer = await connection.QuerySingleOrDefaultAsync<Models.Customer>(
                query,
                new { Email = request.Email });

            if (customer == null)
            {
                logger.LogWarning("Customer not found with Email: {Email}", request.Email);
                return new GetCustomerFromEmailResult
                {
                    Success = false,
                    ErrorMessage = $"Customer with Email {request.Email} not found"
                };
            }

            // Map to DTO
            var customerDto = new CustomerDetailDto
            {
                Id = customer.Id,
                CustomerCode = customer.CustomerCode,
                CompanyName = customer.CompanyName,
                ContactPersonName = customer.ContactPersonName,
                ContactPersonTitle = customer.ContactPersonTitle,
                IdentityNumber = customer.IdentityNumber,
                IdentityIssueDate = customer.IdentityIssueDate,
                IdentityIssuePlace = customer.IdentityIssuePlace,
                Email = customer.Email,
                Phone = customer.Phone,
                AvatarUrl = customer.AvatarUrl,
                Gender = customer.Gender,
                DateOfBirth = customer.DateOfBirth,
                Address = customer.Address,
                City = customer.City,
                District = customer.District,
                Industry = customer.Industry,
                CompanySize = customer.CompanySize,
                Status = customer.Status,
                CustomerSince = customer.CustomerSince,
                FollowsNationalHolidays = customer.FollowsNationalHolidays,
                Notes = customer.Notes,
                CreatedAt = customer.CreatedAt
            };

            logger.LogInformation(
                "Successfully retrieved customer {CustomerCode} with email {Email}",
                customer.CustomerCode,
                customer.Email);

            return new GetCustomerFromEmailResult
            {
                Success = true,
                Customer = customerDto
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting customer by Email: {Email}", request.Email);
            return new GetCustomerFromEmailResult
            {
                Success = false,
                ErrorMessage = $"Error getting customer by email: {ex.Message}"
            };
        }
    }
}
