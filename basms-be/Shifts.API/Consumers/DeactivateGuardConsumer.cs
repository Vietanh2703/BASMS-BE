namespace Shifts.API.Consumers;

/// <summary>
/// Consumer for DeactivateGuardEvent
/// Deactivates Guard record khi contract hết hạn
/// </summary>
public class DeactivateGuardConsumer : IConsumer<DeactivateGuardEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<DeactivateGuardConsumer> _logger;

    public DeactivateGuardConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<DeactivateGuardConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeactivateGuardEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "Received DeactivateGuardEvent for Guard {GuardId}, Email={Email}, Reason={Reason}",
            @event.GuardId,
            @event.Email,
            @event.Reason);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            // Find guard by ID or email
            Guards? guard = null;

            if (@event.GuardId != Guid.Empty)
            {
                guard = await connection.GetAsync<Guards>(@event.GuardId);
            }
            else if (!string.IsNullOrEmpty(@event.Email))
            {
                var guards = await connection.GetAllAsync<Guards>();
                guard = guards.FirstOrDefault(g =>
                    g.Email.Equals(@event.Email, StringComparison.OrdinalIgnoreCase) &&
                    !g.IsDeleted);
            }

            if (guard == null)
            {
                _logger.LogWarning(
                    "Guard not found: GuardId={GuardId}, Email={Email}",
                    @event.GuardId,
                    @event.Email);
                return;
            }

            // Deactivate guard
            guard.IsActive = false;
            guard.EmploymentStatus = "TERMINATED";
            guard.CurrentAvailability = "UNAVAILABLE";
            guard.UpdatedAt = DateTime.UtcNow;

            await connection.UpdateAsync(guard);

            _logger.LogInformation(
                "✓ Deactivated Guard {GuardId}: Email={Email}, Reason={Reason}",
                guard.Id,
                guard.Email,
                @event.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to deactivate Guard {GuardId}",
                @event.GuardId);

            throw; // Re-throw to trigger MassTransit retry
        }
    }
}
