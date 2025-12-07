using BuildingBlocks.Messaging.Events;

namespace Users.API.Consumers;

/// <summary>
/// Consumer xử lý event deactivate user từ Contracts.API
/// Deactivate user khi contract hết hạn
/// </summary>
public class DeactivateUserEventConsumer : IConsumer<DeactivateUserEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<DeactivateUserEventConsumer> _logger;

    public DeactivateUserEventConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<DeactivateUserEventConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeactivateUserEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "Received DeactivateUserEvent for Email={Email}, UserType={UserType}, Reason={Reason}",
            @event.Email,
            @event.UserType,
            @event.Reason);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            // Find user by email
            var user = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT id, email, full_name, status
                FROM users
                WHERE LOWER(email) = LOWER(@Email)
                AND is_deleted = 0",
                new { Email = @event.Email });

            if (user == null)
            {
                _logger.LogWarning(
                    "User not found with email: {Email}. Skipping deactivation.",
                    @event.Email);
                return;
            }

            var userId = (Guid)user.id;

            // Deactivate user
            await connection.ExecuteAsync(@"
                UPDATE users
                SET status = 'inactive',
                    updated_at = @UpdatedAt
                WHERE id = @UserId",
                new
                {
                    UserId = userId,
                    UpdatedAt = DateTime.UtcNow
                });

            _logger.LogInformation(
                "✓ Deactivated user: UserId={UserId}, Email={Email}, UserType={UserType}, Reason={Reason}",
                userId,
                @event.Email,
                @event.UserType,
                @event.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to deactivate user: Email={Email}",
                @event.Email);

            throw; // Re-throw to trigger MassTransit retry
        }
    }
}
