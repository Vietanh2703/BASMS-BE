using BuildingBlocks.Messaging.Events;

namespace Contracts.API.Consumers;

/// <summary>
/// Consumer nhận UserUpdatedEvent từ Users Service
/// Cập nhật customer cache
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

            // Check if customer cache exists
            var existingCache = await connection.GetAsync<CustomerCache>(@event.UserId);

            if (existingCache == null)
            {
                _logger.LogInformation(
                    "Customer cache not found for User {UserId} - skipping update",
                    @event.UserId);
                return;
            }

            // Update customer cache
            existingCache.Email = @event.Email;
            existingCache.FullName = @event.FullName;
            existingCache.Phone = @event.Phone;
            existingCache.AvatarUrl = @event.AvatarUrl;
            existingCache.Address = @event.Address;
            existingCache.LastSyncedAt = DateTime.UtcNow;
            existingCache.SyncStatus = "SYNCED";
            existingCache.UserServiceVersion = @event.Version;
            existingCache.UpdatedAt = DateTime.UtcNow;

            await connection.UpdateAsync(existingCache);

            // Log successful sync
            var log = new CustomerSyncLog
            {
                Id = Guid.NewGuid(),
                UserId = @event.UserId,
                SyncType = "UPDATE",
                SyncStatus = "SUCCESS",
                FieldsChanged = @event.ChangedFields,
                SyncInitiatedBy = "WEBHOOK",
                UserServiceVersionBefore = existingCache.UserServiceVersion ?? 0,
                UserServiceVersionAfter = @event.Version,
                SyncStartedAt = syncStarted,
                SyncCompletedAt = DateTime.UtcNow,
                SyncDurationMs = (int)(DateTime.UtcNow - syncStarted).TotalMilliseconds,
                CreatedAt = DateTime.UtcNow
            };

            await connection.InsertAsync(log);

            _logger.LogInformation(
                "✓ Successfully updated customer cache for User {UserId}",
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
