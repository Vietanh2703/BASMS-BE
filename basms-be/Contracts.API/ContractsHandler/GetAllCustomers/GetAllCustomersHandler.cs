namespace Contracts.API.ContractsHandler.GetAllCustomers;

public record GetAllCustomersQuery : IQuery<GetAllCustomersResult>;

public record CustomerDto
{
    public Guid Id { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string ContactPersonName { get; init; } = string.Empty;
    public string? ContactPersonTitle { get; init; }
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
    public DateTime CustomerSince { get; init; }
    public bool FollowsNationalHolidays { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record GetAllCustomersResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<CustomerDto> Customers { get; init; } = new();
    public int TotalCount { get; init; }
}

internal class GetAllCustomersHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetAllCustomersHandler> logger)
    : IQueryHandler<GetAllCustomersQuery, GetAllCustomersResult>
{
    public async Task<GetAllCustomersResult> Handle(
        GetAllCustomersQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting all customers from database");

            using var connection = await connectionFactory.CreateConnectionAsync();

            var query = @"
                SELECT
                    Id,
                    CustomerCode,
                    CompanyName,
                    ContactPersonName,
                    ContactPersonTitle,
                    Email,
                    Phone,
                    AvatarUrl,
                    Gender,
                    DateOfBirth,
                    Address,
                    City,
                    District,
                    Industry,
                    CompanySize,
                    Status,
                    CustomerSince,
                    FollowsNationalHolidays,
                    CreatedAt
                FROM customers
                WHERE IsDeleted = 0
                ORDER BY CreatedAt DESC
            ";

            var customers = await connection.QueryAsync<Models.Customer>(query);
            var customersList = customers.ToList();

            logger.LogInformation("Found {Count} customers", customersList.Count);
            
            var customerDtos = customersList.Select(c => new CustomerDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                CompanyName = c.CompanyName,
                ContactPersonName = c.ContactPersonName,
                ContactPersonTitle = c.ContactPersonTitle,
                Email = c.Email,
                Phone = c.Phone,
                AvatarUrl = c.AvatarUrl,
                Gender = c.Gender,
                DateOfBirth = c.DateOfBirth,
                Address = c.Address,
                City = c.City,
                District = c.District,
                Industry = c.Industry,
                CompanySize = c.CompanySize,
                Status = c.Status,
                CustomerSince = c.CustomerSince,
                FollowsNationalHolidays = c.FollowsNationalHolidays,
                CreatedAt = c.CreatedAt
            }).ToList();

            return new GetAllCustomersResult
            {
                Success = true,
                Customers = customerDtos,
                TotalCount = customerDtos.Count
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all customers");
            return new GetAllCustomersResult
            {
                Success = false,
                ErrorMessage = $"Error getting customers: {ex.Message}",
                Customers = new List<CustomerDto>()
            };
        }
    }
}
