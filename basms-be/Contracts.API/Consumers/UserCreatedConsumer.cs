using BuildingBlocks.Messaging.Events;

namespace Contracts.API.Consumers;

/// <summary>
/// Consumer nhận UserCreatedEvent từ Users Service
/// Chỉ lưu cache cho users có role = "customer"
/// </summary>
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
            "========================================");
        _logger.LogInformation(
            "Received UserCreatedEvent for User {UserId} with Role {RoleName}",
            @event.UserId,
            @event.RoleName);
        _logger.LogInformation(
            "User details - Email: {Email}, FullName: {FullName}",
            @event.Email,
            @event.FullName);
        _logger.LogInformation(
            "========================================");

        // Chỉ xử lý nếu là customer
        if (@event.RoleName.ToLower() != "customer")
        {
            _logger.LogInformation(
                "User {UserId} has role {RoleName} - not caching (only customer role is cached)",
                @event.UserId,
                @event.RoleName);
            return;
        }

        _logger.LogInformation("Processing customer creation...");

        var syncStarted = DateTime.UtcNow;

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            // Tạo customer cache
            await CreateCustomerCacheAsync(connection, @event);

            // Tạo customer record chính thức
            await CreateCustomerAsync(connection, @event);

            // Log successful sync
            await LogSyncAsync(connection, @event, syncStarted, "SUCCESS", null);

            _logger.LogInformation(
                "✓ Successfully synced customer {UserId} - {Email}",
                @event.UserId,
                @event.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process UserCreatedEvent for Customer {UserId}",
                @event.UserId);

            // Log failed sync
            using var connection = await _dbFactory.CreateConnectionAsync();
            await LogSyncAsync(connection, @event, syncStarted, "FAILED", ex.Message);

            throw; // Re-throw to trigger MassTransit retry
        }
    }

    private async Task CreateCustomerCacheAsync(IDbConnection connection, UserCreatedEvent @event)
    {
        var customerCache = new CustomerCache
        {
            Id = Guid.NewGuid(),
            UserId = @event.UserId,
            FirebaseUid = @event.FirebaseUid,
            Email = @event.Email,
            FullName = @event.FullName,
            Phone = @event.Phone,
            AvatarUrl = @event.AvatarUrl,
            RoleId = @event.RoleId,
            RoleName = @event.RoleName,

            // Customer specific (có thể null nếu chưa có)
            CompanyName = null,
            Address = @event.Address,
            Industry = null,

            // Sync metadata
            LastSyncedAt = DateTime.UtcNow,
            SyncStatus = "SYNCED",
            UserServiceVersion = @event.Version,

            IsActive = true,
            CreatedAt = @event.CreatedAt
        };

        await connection.InsertAsync(customerCache);

        _logger.LogInformation(
            "Created customer cache for User {UserId}: {Email}",
            @event.UserId,
            customerCache.Email);
    }

    private async Task CreateCustomerAsync(IDbConnection connection, UserCreatedEvent @event)
    {
        // Check if customer already exists
        var existingCustomers = await connection.GetAllAsync<Customer>();
        var existingCustomer = existingCustomers.FirstOrDefault(c => c.UserId == @event.UserId && !c.IsDeleted);

        if (existingCustomer != null)
        {
            _logger.LogInformation(
                "Customer already exists for User {UserId}, skipping creation",
                @event.UserId);
            return;
        }

        // Generate CustomerCode (CUST-001, CUST-002...)
        var customerCode = await GenerateCustomerCodeAsync(connection);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            UserId = @event.UserId,
            CustomerCode = customerCode,

            // Company info - sử dụng FullName làm CompanyName tạm thời
            // Customer có thể update sau
            CompanyName = @event.FullName,
            ContactPersonName = @event.FullName,
            ContactPersonTitle = null,

            Email = @event.Email,
            Phone = @event.Phone ?? "",
            Address = @event.Address ?? "",
            City = null,
            District = null,
            Industry = null,
            CompanySize = null,

            CustomerSince = DateTime.UtcNow,
            Status = "active",
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
            "✓ Created customer record {CustomerCode} for User {UserId}",
            customerCode,
            @event.UserId);
    }

    private async Task<string> GenerateCustomerCodeAsync(IDbConnection connection)
    {
        // Lấy tất cả customers và tìm code lớn nhất
        var customers = await connection.GetAllAsync<Customer>();
        var existingCodes = customers
            .Where(c => c.CustomerCode.StartsWith("CUST-"))
            .Select(c => c.CustomerCode)
            .ToList();

        if (!existingCodes.Any())
        {
            return "CUST-001";
        }

        // Parse số cuối cùng và tăng lên
        var maxNumber = existingCodes
            .Select(code =>
            {
                var parts = code.Split('-');
                return parts.Length == 2 && int.TryParse(parts[1], out var num) ? num : 0;
            })
            .Max();

        var newNumber = maxNumber + 1;
        return $"CUST-{newNumber:D3}"; // D3 = 3 digits: 001, 002, 003...
    }

    private async Task LogSyncAsync(
        IDbConnection connection,
        UserCreatedEvent @event,
        DateTime syncStarted,
        string status,
        string? errorMessage)
    {
        var log = new CustomerSyncLog
        {
            Id = Guid.NewGuid(),
            UserId = @event.UserId,
            SyncType = "CREATE",
            SyncStatus = status,
            SyncInitiatedBy = "WEBHOOK",
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
