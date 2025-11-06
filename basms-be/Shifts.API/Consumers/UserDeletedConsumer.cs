using BuildingBlocks.Messaging.Events;
using MassTransit;
using Shifts.API.Models;

namespace Shifts.API.Consumers;

/// <summary>
/// Consumer for UserDeletedEvent
/// Soft-deletes manager/guard cache when user is deleted
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
            "Received UserDeletedEvent for User {UserId} with Role {RoleName}",
            @event.UserId,
            @event.RoleName);

        var syncStarted = DateTime.UtcNow;

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            var isManager = @event.RoleName.ToLower() is "manager" or "director" or "supervisor";
            var isGuard = @event.RoleName.ToLower() == "guard";

            if (isManager)
            {
                await SoftDeleteManagerAsync(connection, @event);
            }
            else if (isGuard)
            {
                await SoftDeleteGuardAsync(connection, @event);
            }

            // Log sync
            await LogSyncAsync(connection, @event, syncStarted, "SUCCESS", null);

            _logger.LogInformation(
                "Successfully soft-deleted cache for User {UserId}",
                @event.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process UserDeletedEvent for User {UserId}",
                @event.UserId);

            using var connection = await _dbFactory.CreateConnectionAsync();
            await LogSyncAsync(connection, @event, syncStarted, "FAILED", ex.Message);

            throw;
        }
    }

    private async Task SoftDeleteManagerAsync(IDbConnection connection, UserDeletedEvent @event)
    {
        var manager = await connection.GetAsync<Managers>(@event.UserId);
        if (manager == null)
        {
            _logger.LogWarning(
                "Manager cache not found for User {UserId}",
                @event.UserId);
            return;
        }

        manager.IsDeleted = true;
        manager.DeletedAt = @event.DeletedAt;
        manager.IsActive = false;
        manager.EmploymentStatus = "TERMINATED";

        await connection.UpdateAsync(manager);

        _logger.LogInformation(
            "Soft-deleted manager cache for User {UserId}",
            @event.UserId);
    }

    private async Task SoftDeleteGuardAsync(IDbConnection connection, UserDeletedEvent @event)
    {
        var guard = await connection.GetAsync<Guards>(@event.UserId);
        if (guard == null)
        {
            _logger.LogWarning(
                "Guard cache not found for User {UserId}",
                @event.UserId);
            return;
        }

        guard.IsDeleted = true;
        guard.DeletedAt = @event.DeletedAt;
        guard.IsActive = false;
        guard.EmploymentStatus = "TERMINATED";
        guard.TerminationDate = @event.DeletedAt;
        guard.TerminationReason = @event.DeletionReason;

        await connection.UpdateAsync(guard);

        _logger.LogInformation(
            "Soft-deleted guard cache for User {UserId}",
            @event.UserId);
    }

    private async Task LogSyncAsync(
        IDbConnection connection,
        UserDeletedEvent @event,
        DateTime syncStarted,
        string status,
        string? errorMessage)
    {
        var log = new UserSyncLog
        {
            Id = Guid.NewGuid(),
            UserId = @event.UserId,
            UserType = @event.RoleName.ToLower() is "manager" or "director" or "supervisor" ? "MANAGER" : "GUARD",
            SyncType = "DELETE",
            SyncStatus = status,
            SyncInitiatedBy = "WEBHOOK",
            ErrorMessage = errorMessage,
            SyncStartedAt = syncStarted,
            SyncCompletedAt = DateTime.UtcNow,
            SyncDurationMs = (int)(DateTime.UtcNow - syncStarted).TotalMilliseconds,
            CreatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(log);
    }
}
