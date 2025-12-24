namespace Shifts.API.Consumers;

public class UpdateGuardInfoConsumer : IConsumer<UpdateGuardInfoEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<UpdateGuardInfoConsumer> _logger;

    public UpdateGuardInfoConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<UpdateGuardInfoConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UpdateGuardInfoEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "Received UpdateGuardInfoEvent for Guard {GuardId}: Level={Level}, Wage={Wage}",
            @event.GuardId,
            @event.CertificationLevel,
            @event.StandardWage);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();
            
            var guard = await connection.GetAsync<Guards>(@event.GuardId);

            if (guard == null)
            {
                _logger.LogWarning(
                    "Guard {GuardId} not found. Skipping update.",
                    @event.GuardId);
                return;
            }
            
            guard.CertificationLevel = @event.CertificationLevel;
            guard.StandardWage = @event.StandardWage;
            guard.UpdatedAt = DateTime.UtcNow;

            await connection.UpdateAsync(guard);

            _logger.LogInformation(
                "✓ Updated Guard {GuardId}: CertificationLevel={Level}, StandardWage={Wage}",
                @event.GuardId,
                @event.CertificationLevel,
                @event.StandardWage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process UpdateGuardInfoEvent for Guard {GuardId}",
                @event.GuardId);

            throw;
        }
    }
}
