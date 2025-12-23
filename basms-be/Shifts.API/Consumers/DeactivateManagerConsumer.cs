namespace Shifts.API.Consumers;

public class DeactivateManagerConsumer : IConsumer<DeactivateManagerEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<DeactivateManagerConsumer> _logger;

    public DeactivateManagerConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<DeactivateManagerConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeactivateManagerEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "Received DeactivateManagerEvent for Manager {ManagerId}, Email={Email}, Reason={Reason}",
            @event.ManagerId,
            @event.Email,
            @event.Reason);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();
            Managers? manager = null;

            if (@event.ManagerId != Guid.Empty)
            {
                manager = await connection.GetAsync<Managers>(@event.ManagerId);
            }
            else if (!string.IsNullOrEmpty(@event.Email))
            {
                var managers = await connection.GetAllAsync<Managers>();
                manager = managers.FirstOrDefault(m =>
                    m.Email.Equals(@event.Email, StringComparison.OrdinalIgnoreCase) &&
                    !m.IsDeleted);
            }

            if (manager == null)
            {
                _logger.LogWarning(
                    "Manager not found: ManagerId={ManagerId}, Email={Email}",
                    @event.ManagerId,
                    @event.Email);
                return;
            }
            
            manager.IsActive = false;
            manager.EmploymentStatus = "TERMINATED";
            manager.UpdatedAt = DateTime.UtcNow;

            await connection.UpdateAsync(manager);

            _logger.LogInformation(
                "✓ Deactivated Manager {ManagerId}: Email={Email}, Reason={Reason}",
                manager.Id,
                manager.Email,
                @event.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to deactivate Manager {ManagerId}",
                @event.ManagerId);

            throw;
        }
    }
}
