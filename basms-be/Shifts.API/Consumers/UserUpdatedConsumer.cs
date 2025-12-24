namespace Shifts.API.Consumers;

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
            "Received UserUpdatedEvent for User {UserId}. Changed fields: {Fields}",
            @event.UserId,
            string.Join(", ", @event.ChangedFields));

        var syncStarted = DateTime.UtcNow;

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            var roleLower = @event.RoleName.ToLower();
            var isManager = roleLower == "manager";
            var isGuard = roleLower == "guard";

            if (isManager)
            {
                await UpdateManagerCacheAsync(connection, @event);
            }
            else if (isGuard)
            {
                await UpdateGuardCacheAsync(connection, @event);
            }
            else
            {
                _logger.LogInformation(
                    "User {UserId} role {RoleName} not cached - skipping update",
                    @event.UserId,
                    @event.RoleName);
                return;
            }

            // Log sync
            await LogSyncAsync(connection, @event, syncStarted, "SUCCESS", null);

            _logger.LogInformation(
                "Successfully updated cache for User {UserId}",
                @event.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process UserUpdatedEvent for User {UserId}",
                @event.UserId);

            using var connection = await _dbFactory.CreateConnectionAsync();
            await LogSyncAsync(connection, @event, syncStarted, "FAILED", ex.Message);

            throw;
        }
    }

    private async Task UpdateManagerCacheAsync(IDbConnection connection, UserUpdatedEvent @event)
    {
        // Fetch existing manager
        var manager = await connection.GetAsync<Managers>(@event.UserId);
        if (manager == null)
        {
            _logger.LogWarning(
                "Manager cache not found for User {UserId} - may need full sync",
                @event.UserId);
            return;
        }
        
        manager.AvatarUrl = @event.AvatarUrl;
        manager.PhoneNumber = @event.Phone;
        manager.EmploymentStatus = MapStatus(@event.Status);
        manager.LastSyncedAt = DateTime.UtcNow;
        manager.SyncStatus = "SYNCED";
        manager.UserServiceVersion = @event.Version;
        manager.UpdatedAt = @event.UpdatedAt;

        await connection.UpdateAsync(manager);

        _logger.LogInformation(
            "Updated manager cache for User {UserId}",
            @event.UserId);
    }

    private async Task UpdateGuardCacheAsync(IDbConnection connection, UserUpdatedEvent @event)
    {
        var guard = await connection.GetAsync<Guards>(@event.UserId);
        if (guard == null)
        {
            _logger.LogWarning(
                "Guard cache not found for User {UserId} - may need full sync",
                @event.UserId);
            return;
        }
        
        guard.AvatarUrl = @event.AvatarUrl ?? guard.AvatarUrl;
        guard.PhoneNumber = @event.Phone ?? guard.PhoneNumber;
        guard.CurrentAddress = @event.Address;
        guard.EmploymentStatus = MapStatus(@event.Status);
        guard.ContractType = @event.ContractType;
        guard.TerminationDate = @event.TerminationDate;
        guard.TerminationReason = @event.TerminationReason;
        guard.LastSyncedAt = DateTime.UtcNow;
        guard.SyncStatus = "SYNCED";
        guard.UserServiceVersion = @event.Version;
        guard.UpdatedAt = @event.UpdatedAt;

        await connection.UpdateAsync(guard);

        _logger.LogInformation(
            "Updated guard cache for User {UserId}",
            @event.UserId);
    }

    private string MapStatus(string userStatus)
    {
        return userStatus.ToLower() switch
        {
            "active" => "ACTIVE",
            "inactive" => "SUSPENDED",
            "suspended" => "SUSPENDED",
            _ => "ACTIVE"
        };
    }

    private async Task LogSyncAsync(
        IDbConnection connection,
        UserUpdatedEvent @event,
        DateTime syncStarted,
        string status,
        string? errorMessage)
    {
        var log = new UserSyncLog
        {
            Id = Guid.NewGuid(),
            UserId = @event.UserId,
            UserType = @event.RoleName.ToLower() switch
            {
                "manager" => "MANAGER",
                "guard" => "GUARD",
                _ => "UNKNOWN"
            },
            SyncType = "UPDATE",
            SyncStatus = status,
            FieldsChanged = JsonSerializer.Serialize(@event.ChangedFields),
            SyncInitiatedBy = "WEBHOOK",
            UserServiceVersionBefore = @event.Version - 1,
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
