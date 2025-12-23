namespace Contracts.API.Consumers;

public class UserUpdatedConsumer : IConsumer<UserUpdatedEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<UserUpdatedConsumer> _logger;

    public UserUpdatedConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<UserUpdatedConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserUpdatedEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "Received UserUpdatedEvent for User {UserId}",
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
                    "Customer not found for User {UserId} - skipping update",
                    @event.UserId);
                return;
            }

            bool customerUpdated = false;
            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();
            

            if (@event.ChangedFields.Contains("Phone") && existingCustomer.Phone != @event.Phone)
            {
                oldValues["Phone"] = existingCustomer.Phone;
                newValues["Phone"] = @event.Phone;
                existingCustomer.Phone = @event.Phone ?? "";
                customerUpdated = true;
            }

            if (@event.ChangedFields.Contains("Address") && existingCustomer.Address != @event.Address)
            {
                oldValues["Address"] = existingCustomer.Address;
                newValues["Address"] = @event.Address;
                existingCustomer.Address = @event.Address ?? "";
                customerUpdated = true;
            }

            if (@event.ChangedFields.Contains("FullName") && existingCustomer.ContactPersonName != @event.FullName)
            {
                oldValues["ContactPersonName"] = existingCustomer.ContactPersonName;
                newValues["ContactPersonName"] = @event.FullName;
                existingCustomer.ContactPersonName = @event.FullName;
                customerUpdated = true;
            }

            if (customerUpdated)
            {
                existingCustomer.UpdatedAt = DateTime.UtcNow;
                await connection.UpdateAsync(existingCustomer);
                _logger.LogInformation(
                    "Updated customer record for User {UserId}",
                    @event.UserId);
            }

            var log = new CustomerSyncLog
            {
                Id = Guid.NewGuid(),
                UserId = @event.UserId,
                SyncType = "UPDATE",
                SyncStatus = "SUCCESS",
                FieldsChanged = JsonSerializer.Serialize(@event.ChangedFields),
                OldValues = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null,
                SyncInitiatedBy = "WEBHOOK",
                RetryCount = 0,
                UserServiceVersionAfter = @event.Version,
                SyncStartedAt = syncStarted,
                SyncCompletedAt = DateTime.UtcNow,
                SyncDurationMs = (int)(DateTime.UtcNow - syncStarted).TotalMilliseconds,
                CreatedAt = DateTime.UtcNow
            };

            await connection.InsertAsync(log);

            _logger.LogInformation(
                "Successfully logged customer update for User {UserId}",
                @event.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process UserUpdatedEvent for User {UserId}",
                @event.UserId);

            throw;
        }
    }
}
