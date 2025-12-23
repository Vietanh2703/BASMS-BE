namespace Contracts.API.Consumers;

public class UserCreatedConsumer : IConsumer<UserCreatedEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<UserCreatedConsumer> _logger;

    public UserCreatedConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<UserCreatedConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        var @event = context.Message;
        _logger.LogInformation(
            "Received UserCreatedEvent for User {UserId} with Role {RoleName}",
            @event.UserId,
            @event.RoleName);
        _logger.LogInformation(
            "User details - Email: {Email}, FullName: {FullName}",
            @event.Email,
            @event.FullName);
        
        if (@event.RoleName.ToLower() != "customer")
        {
            _logger.LogInformation(
                "User {UserId} has role {RoleName} - skipping (only customer role is processed)",
                @event.UserId,
                @event.RoleName);
            return;
        }

        _logger.LogInformation("Processing customer creation...");

        var syncStarted = DateTime.UtcNow;

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();
            var existingCustomer = await connection.QueryFirstOrDefaultAsync<Customer>(
                "SELECT * FROM customers WHERE UserId = @UserId AND IsDeleted = 0 LIMIT 1",
                new { UserId = @event.UserId });

            if (existingCustomer == null)
            {
                await CreateCustomerAsync(connection, @event);
            }
            else
            {
                _logger.LogInformation(
                    "Customer record already exists for User {UserId}, skipping creation",
                    @event.UserId);
            }
            
            await LogSyncAsync(connection, @event, syncStarted, "SUCCESS", null, existingCustomer != null);

            _logger.LogInformation(
                "Successfully synced customer {UserId} - {Email}",
                @event.UserId,
                @event.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process UserCreatedEvent for Customer {UserId}",
                @event.UserId);
            using var connection = await _dbFactory.CreateConnectionAsync();
            await LogSyncAsync(connection, @event, syncStarted, "FAILED", ex.Message, false);

            throw;
        }
    }

    private async Task CreateCustomerAsync(IDbConnection connection, UserCreatedEvent @event)
    {
        var existingCustomer = await connection.QueryFirstOrDefaultAsync<Customer>(
            "SELECT * FROM customers WHERE UserId = @UserId AND IsDeleted = 0 LIMIT 1",
            new { UserId = @event.UserId });

        if (existingCustomer != null)
        {
            _logger.LogInformation(
                "Customer already exists for User {UserId}, skipping creation",
                @event.UserId);
            return;
        }
        
        try
        {
            var customerCode = await GenerateCustomerCodeAsync(connection);

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                UserId = @event.UserId,
                CustomerCode = customerCode,
                CompanyName = null,
                ContactPersonName = @event.FullName,
                ContactPersonTitle = null,
                IdentityNumber = @event.IdentityNumber,
                IdentityIssueDate = @event.IdentityIssueDate,
                IdentityIssuePlace = @event.IdentityIssuePlace,
                Email = @event.Email,
                Phone = @event.Phone ?? "",
                AvatarUrl = @event.AvatarUrl,
                Gender = @event.Gender,
                DateOfBirth = @event.DateOfBirth,
                Address = @event.Address ?? "",
                City = null,
                District = null,
                Industry = null,
                CompanySize = null,
                CustomerSince = DateTime.UtcNow,
                Status = "in-active",
                FollowsNationalHolidays = true,
                Notes = "Auto-created from Users Service",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null,
                CreatedBy = null,
                UpdatedBy = null
            };

            await connection.InsertAsync(customer);

            _logger.LogInformation(
                "Created customer record {CustomerCode} for User {UserId}",
                customerCode,
                @event.UserId);
        }
        catch (MySqlException ex) when (ex.Number == 1062) 
        {
            _logger.LogWarning(
                "Duplicate key when creating customer for User {UserId}. " +
                "Customer was already created by ImportContractFromDocumentHandler. This is expected in race conditions.",
                @event.UserId);
            
            var justCreated = await connection.QueryFirstOrDefaultAsync<Customer>(
                "SELECT * FROM customers WHERE UserId = @UserId AND IsDeleted = 0 LIMIT 1",
                new { UserId = @event.UserId });

            if (justCreated != null)
            {
                _logger.LogInformation(
                    "Customer already created by another process: {CustomerCode} for User {UserId}",
                    justCreated.CustomerCode,
                    @event.UserId);
            }
            else
            {
                _logger.LogError(
                    "Duplicate key error but customer not found for User {UserId}. Re-throwing exception.",
                    @event.UserId);
                throw;
            }
        }
    }

    private async Task<string> GenerateCustomerCodeAsync(IDbConnection connection)
    {
        var customers = await connection.GetAllAsync<Customer>();
        var existingCodes = customers
            .Where(c => c.CustomerCode.StartsWith("CUST-"))
            .Select(c => c.CustomerCode)
            .ToList();

        if (!existingCodes.Any())
        {
            return "CUST-001";
        }
        
        var maxNumber = existingCodes
            .Select(code =>
            {
                var parts = code.Split('-');
                return parts.Length == 2 && int.TryParse(parts[1], out var num) ? num : 0;
            })
            .Max();

        var newNumber = maxNumber + 1;
        return $"CUST-{newNumber:D3}";
    }

    private async Task LogSyncAsync(
        IDbConnection connection,
        UserCreatedEvent @event,
        DateTime syncStarted,
        string status,
        string? errorMessage,
        bool customerAlreadyExists)
    {
        string? fieldsChangedJson = null;
        if (!customerAlreadyExists)
        {
            fieldsChangedJson = JsonSerializer.Serialize(new[] { "CompanyName", "Email", "Phone", "Address", "ContactPersonName" });
        }

        var log = new CustomerSyncLog
        {
            Id = Guid.NewGuid(),
            UserId = @event.UserId,
            SyncType = "CREATE",
            SyncStatus = status,
            FieldsChanged = fieldsChangedJson,
            NewValues = status == "SUCCESS" && !customerAlreadyExists ? JsonSerializer.Serialize(new
            {
                CompanyName = @event.FullName,
                Email = @event.Email,
                Phone = @event.Phone,
                Address = @event.Address,
                ContactPersonName = @event.FullName
            }) : null,
            SyncInitiatedBy = "WEBHOOK",
            RetryCount = 0,
            UserServiceVersionAfter = @event.Version,
            ErrorMessage = errorMessage,
            SyncStartedAt = syncStarted,
            SyncCompletedAt = DateTime.UtcNow,
            SyncDurationMs = (int)(DateTime.UtcNow - syncStarted).TotalMilliseconds,
            CreatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(log);
    }
}
