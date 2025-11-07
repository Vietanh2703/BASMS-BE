using BuildingBlocks.Messaging.Events;

namespace Contracts.API.Consumers;

/// <summary>
/// Consumer nhận UserDeletedEvent từ Users Service
/// Đánh dấu customer cache là inactive (soft delete)
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

            // Check if customer cache exists
            var existingCache = await connection.GetAsync<CustomerCache>(@event.UserId);

            if (existingCache == null)
            {
                _logger.LogInformation(
                    "Customer cache not found for User {UserId} - skipping delete",
                    @event.UserId);
                return;
            }

            // Soft delete - set IsActive = false
            existingCache.IsActive = false;
            existingCache.LastSyncedAt = DateTime.UtcNow;
            existingCache.SyncStatus = "SYNCED";
            existingCache.UpdatedAt = DateTime.UtcNow;

            await connection.UpdateAsync(existingCache);

            // Log successful sync
            var log = new CustomerSyncLog
            {
                Id = Guid.NewGuid(),
                UserId = @event.UserId,
                SyncType = "DELETE",
                SyncStatus = "SUCCESS",
                SyncInitiatedBy = "WEBHOOK",
                SyncStartedAt = syncStarted,
                SyncCompletedAt = DateTime.UtcNow,
                SyncDurationMs = (int)(DateTime.UtcNow - syncStarted).TotalMilliseconds,
                CreatedAt = DateTime.UtcNow
            };

            await connection.InsertAsync(log);

            _logger.LogInformation(
                "✓ Successfully marked customer cache as inactive for User {UserId}",
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
