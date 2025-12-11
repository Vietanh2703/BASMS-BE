namespace Shifts.API.Consumers;

/// <summary>
/// Consumer for UpdateManagerInfoEvent
/// Updates Manager's CertificationLevel and StandardWage when working contract is imported
/// </summary>
public class UpdateManagerInfoConsumer : IConsumer<UpdateManagerInfoEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<UpdateManagerInfoConsumer> _logger;

    public UpdateManagerInfoConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<UpdateManagerInfoConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UpdateManagerInfoEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "Received UpdateManagerInfoEvent for Manager {ManagerId}: Level={Level}, Wage={Wage}, TotalGuards={TotalGuards}",
            @event.ManagerId,
            @event.CertificationLevel,
            @event.StandardWage,
            @event.TotalGuardsSupervised);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            // Check if manager exists
            var manager = await connection.GetAsync<Managers>(@event.ManagerId);

            if (manager == null)
            {
                _logger.LogWarning(
                    "Manager {ManagerId} not found. Skipping update.",
                    @event.ManagerId);
                return;
            }

            // Update manager with new info
            manager.CertificationLevel = @event.CertificationLevel;
            manager.StandardWage = @event.StandardWage;
            manager.TotalGuardsSupervised = @event.TotalGuardsSupervised ?? manager.TotalGuardsSupervised;
            manager.UpdatedAt = DateTime.UtcNow;

            await connection.UpdateAsync(manager);

            _logger.LogInformation(
                "✓ Updated Manager {ManagerId}: CertificationLevel={Level}, StandardWage={Wage}, TotalGuardsSupervised={TotalGuards}",
                @event.ManagerId,
                @event.CertificationLevel,
                @event.StandardWage,
                @event.TotalGuardsSupervised);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process UpdateManagerInfoEvent for Manager {ManagerId}",
                @event.ManagerId);

            throw; // Re-throw to trigger MassTransit retry
        }
    }
}
