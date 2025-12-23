namespace Contracts.API.Consumers;

public class UserDeletedConsumer : IConsumer<UserDeletedEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<UserDeletedConsumer> _logger;

    public UserDeletedConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<UserDeletedConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserDeletedEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "Received UserDeletedEvent for User {UserId}",
            @event.UserId);

        var syncStarted = DateTime.UtcNow;

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();
            var existingCustomer = await connection.QueryFirstOrDefaultAsync<Customer>(
                "SELECT * FROM customers WHERE UserId = @UserId AND IsDeleted = 0 LIMIT 1",
                new { UserId = @event.UserId });

            if (existingCustomer == null)
            {
                _logger.LogInformation(
                    "Customer not found for User {UserId} - skipping delete",
                    @event.UserId);
                return;
            }
            
            existingCustomer.IsDeleted = true;
            existingCustomer.Status = "inactive";
            existingCustomer.UpdatedAt = DateTime.UtcNow;

            await connection.UpdateAsync(existingCustomer);

            var log = new CustomerSyncLog
            {
                Id = Guid.NewGuid(),
                UserId = @event.UserId,
                SyncType = "DELETE",
                SyncStatus = "SUCCESS",
                FieldsChanged = JsonSerializer.Serialize(new[] { "IsDeleted", "Status" }),
                OldValues = JsonSerializer.Serialize(new { IsDeleted = false, Status = existingCustomer.Status }),
                NewValues = JsonSerializer.Serialize(new { IsDeleted = true, Status = "inactive" }),
                SyncInitiatedBy = "WEBHOOK",
                RetryCount = 0,
                SyncStartedAt = syncStarted,
                SyncCompletedAt = DateTime.UtcNow,
                SyncDurationMs = (int)(DateTime.UtcNow - syncStarted).TotalMilliseconds,
                CreatedAt = DateTime.UtcNow
            };

            await connection.InsertAsync(log);

            _logger.LogInformation(
                "Successfully marked customer as deleted for User {UserId}",
                @event.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process UserDeletedEvent for User {UserId}",
                @event.UserId);

            throw;
        }
    }
}
