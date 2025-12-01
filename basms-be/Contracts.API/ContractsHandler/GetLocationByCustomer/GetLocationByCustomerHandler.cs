namespace Contracts.API.ContractsHandler.GetLocationByCustomer;

// ================================================================
// QUERY & RESULT
// ================================================================

/// <summary>
/// Query để lấy danh sách locations theo customer ID
/// </summary>
public record GetLocationByCustomerQuery(Guid CustomerId) : IQuery<GetLocationByCustomerResult>;

/// <summary>
/// DTO cho Location info
/// </summary>
public record LocationDto
{
    public Guid Id { get; init; }
    public string LocationCode { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? District { get; init; }
    public string? Ward { get; init; }
    public string LocationType { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

/// <summary>
/// Kết quả query
/// </summary>
public record GetLocationByCustomerResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerCode { get; init; }
    public List<LocationDto> Locations { get; init; } = new();
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// Handler để lấy danh sách locations theo customer ID
/// </summary>
internal class GetLocationByCustomerHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetLocationByCustomerHandler> logger)
    : IQueryHandler<GetLocationByCustomerQuery, GetLocationByCustomerResult>
{
    public async Task<GetLocationByCustomerResult> Handle(
        GetLocationByCustomerQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting locations for customer: {CustomerId}", request.CustomerId);

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
                return new GetLocationByCustomerResult
                {
                    Success = false,
                    ErrorMessage = $"Customer with ID {request.CustomerId} not found"
                };
            }

            // ================================================================
            // 2. GET CUSTOMER LOCATIONS
            // ================================================================
            var locationsQuery = @"
                SELECT
                    Id,
                    LocationCode,
                    LocationName,
                    Address,
                    City,
                    District,
                    Ward,
                    LocationType,
                    IsActive
                FROM customer_locations
                WHERE CustomerId = @CustomerId AND IsDeleted = 0
                ORDER BY LocationCode
            ";

            var locations = await connection.QueryAsync<LocationDto>(
                locationsQuery,
                new { CustomerId = request.CustomerId });

            var locationsList = locations.ToList();

            logger.LogInformation(
                "Successfully retrieved {Count} location(s) for customer {CustomerCode}",
                locationsList.Count, customerCode);

            return new GetLocationByCustomerResult
            {
                Success = true,
                CustomerId = request.CustomerId,
                CustomerCode = customerCode,
                Locations = locationsList
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting locations for customer: {CustomerId}", request.CustomerId);
            return new GetLocationByCustomerResult
            {
                Success = false,
                ErrorMessage = $"Error getting locations: {ex.Message}"
            };
        }
    }
}
