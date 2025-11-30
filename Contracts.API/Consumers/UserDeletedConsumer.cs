using BuildingBlocks.Messaging.Events;

namespace Contracts.API.Consumers;

/// <summary>
/// Consumer nhận UserDeletedEvent từ Users Service
/// Đánh dấu customer là deleted (soft delete) và log sync
/// </summary>
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

            // Check if customer exists
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

            // Soft delete - set IsDeleted = true, Status = inactive
            existingCustomer.IsDeleted = true;
            existingCustomer.Status = "inactive";
            existingCustomer.UpdatedAt = DateTime.UtcNow;

            await connection.UpdateAsync(existingCustomer);

            // Log successful sync
            var log = new CustomerSyncLog
            {
                Id = Guid.NewGuid(),
                UserId = @event.UserId,
                SyncType = "DELETE",
                SyncStatus = "SUCCESS",
                FieldsChanged = System.Text.Json.JsonSerializer.Serialize(new[] { "IsDeleted", "Status" }),
                OldValues = System.Text.Json.JsonSerializer.Serialize(new { IsDeleted = false, Status = existingCustomer.Status }),
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { IsDeleted = true, Status = "inactive" }),
                SyncInitiatedBy = "WEBHOOK",
                RetryCount = 0,
                SyncStartedAt = syncStarted,
                SyncCompletedAt = DateTime.UtcNow,
                SyncDurationMs = (int)(DateTime.UtcNow - syncStarted).TotalMilliseconds,
                CreatedAt = DateTime.UtcNow
            };

            await connection.InsertAsync(log);

            _logger.LogInformation(
                "✓ Successfully marked customer as deleted for User {UserId}",
                @event.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process UserDeletedEvent for User {UserId}",
                @event.UserId);

            throw; // Re-throw to trigger MassTransit retry
        }
    }
}
