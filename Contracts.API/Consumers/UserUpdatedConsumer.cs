using BuildingBlocks.Messaging.Events;

namespace Contracts.API.Consumers;

/// <summary>
/// Consumer nhận UserUpdatedEvent từ Users Service
/// Log customer update sync
/// </summary>
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

            // Check if customer exists
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

            // Update customer record if needed
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
                    "✓ Updated customer record for User {UserId}",
                    @event.UserId);
            }

            // Log sync
            var log = new CustomerSyncLog
            {
                Id = Guid.NewGuid(),
                UserId = @event.UserId,
                SyncType = "UPDATE",
                SyncStatus = "SUCCESS",
                FieldsChanged = System.Text.Json.JsonSerializer.Serialize(@event.ChangedFields),
                OldValues = oldValues.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(newValues) : null,
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
                "✓ Successfully logged customer update for User {UserId}",
                @event.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process UserUpdatedEvent for User {UserId}",
                @event.UserId);

            throw; // Re-throw to trigger MassTransit retry
        }
    }
}
