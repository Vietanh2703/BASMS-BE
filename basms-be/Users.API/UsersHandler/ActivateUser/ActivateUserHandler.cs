namespace Users.API.UsersHandler.ActivateUser;

public record ActivateUserCommand(
    Guid UserId,
    Guid? ActivatedBy = null 
) : ICommand<ActivateUserResult>;

public record ActivateUserResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public Guid? UserId { get; init; }
    public string? Email { get; init; }
    public string? FullName { get; init; }
}

public class ActivateUserHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ActivateUserHandler> logger,
    Messaging.UserEventPublisher eventPublisher)
    : ICommandHandler<ActivateUserCommand, ActivateUserResult>
{
    public async Task<ActivateUserResult> Handle(
        ActivateUserCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Activating user: {UserId}", command.UserId);

            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var user = await connection.GetAsync<Models.Users>(command.UserId, transaction);

                if (user == null)
                {
                    logger.LogWarning("User not found: {UserId}", command.UserId);
                    return new ActivateUserResult
                    {
                        Success = false,
                        Message = $"User with ID {command.UserId} not found"
                    };
                }
                
                if (user.IsDeleted)
                {
                    logger.LogWarning("Cannot activate deleted user: {UserId}", command.UserId);
                    return new ActivateUserResult
                    {
                        Success = false,
                        Message = "Cannot activate a deleted user"
                    };
                }
                
                if (user.IsActive)
                {
                    logger.LogInformation("User is already active: {UserId}", command.UserId);
                    return new ActivateUserResult
                    {
                        Success = true,
                        Message = "User is already active",
                        UserId = user.Id,
                        Email = user.Email,
                        FullName = user.FullName
                    };
                }
                
                user.IsActive = true;
                user.UpdatedAt = DateTime.UtcNow;

                await connection.UpdateAsync(user, transaction);
                logger.LogInformation("User activated in database: {UserId}", command.UserId);
                
                await LogAuditAsync(connection, transaction, user, command.ActivatedBy);
                
                transaction.Commit();
                logger.LogInformation("Transaction committed for user activation: {UserId}", command.UserId);

                logger.LogInformation(
                    "User activated successfully: {UserId} - {Email}",
                    user.Id, user.Email);

                return new ActivateUserResult
                {
                    Success = true,
                    Message = "User activated successfully",
                    UserId = user.Id,
                    Email = user.Email,
                    FullName = user.FullName
                };
            }
            catch
            {
                transaction.Rollback();
                logger.LogWarning("Transaction rolled back due to error");
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error activating user: {UserId}", command.UserId);
            return new ActivateUserResult
            {
                Success = false,
                Message = $"Error activating user: {ex.Message}"
            };
        }
    }
    
    private async Task LogAuditAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Models.Users user,
        Guid? activatedBy)
    {
        var auditLog = new AuditLogs
        {
            Id = Guid.NewGuid(),
            UserId = activatedBy ?? user.Id,
            Action = "ACTIVATE_USER",
            EntityType = "User",
            EntityId = user.Id,
            OldValues = JsonSerializer.Serialize(new
            {
                IsActive = false
            }),
            NewValues = JsonSerializer.Serialize(new
            {
                IsActive = true,
                ActivatedAt = DateTime.UtcNow,
                ActivatedBy = activatedBy
            }),
            Status = "success",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(auditLog, transaction);
    }
}
