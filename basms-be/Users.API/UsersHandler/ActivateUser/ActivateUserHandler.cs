// Handler xử lý logic kích hoạt user
// Set IsActive = true, gửi email thông báo, và log audit trail

namespace Users.API.UsersHandler.ActivateUser;

// Command chứa dữ liệu để activate user
public record ActivateUserCommand(
    Guid UserId,
    Guid? ActivatedBy = null  // ID của admin/manager thực hiện activate
) : ICommand<ActivateUserResult>;

// Result trả về từ Handler
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
    Users.API.Messaging.UserEventPublisher eventPublisher)
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
                // Bước 1: Tìm user trong database
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

                // Bước 2: Kiểm tra user đã bị xóa chưa
                if (user.IsDeleted)
                {
                    logger.LogWarning("Cannot activate deleted user: {UserId}", command.UserId);
                    return new ActivateUserResult
                    {
                        Success = false,
                        Message = "Cannot activate a deleted user"
                    };
                }

                // Bước 3: Kiểm tra user đã active chưa
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

                // Bước 4: Activate user
                user.IsActive = true;
                user.UpdatedAt = DateTime.UtcNow;

                await connection.UpdateAsync(user, transaction);
                logger.LogInformation("User activated in database: {UserId}", command.UserId);

                // Bước 5: Log audit trail
                await LogAuditAsync(connection, transaction, user, command.ActivatedBy);

                // Bước 6: Commit transaction
                transaction.Commit();
                logger.LogInformation("Transaction committed for user activation: {UserId}", command.UserId);

                // Bước 7: Publish UserActivatedEvent (optional - commented out for now)
                // try
                // {
                //     // Có thể publish event để các service khác biết user đã được activate
                //     await eventPublisher.PublishUserActivatedAsync(user, cancellationToken);
                // }
                // catch (Exception eventEx)
                // {
                //     logger.LogError(eventEx,
                //         "Failed to publish UserActivatedEvent for user {UserId}, but activation was successful",
                //         user.Id);
                // }

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

    // Hàm ghi log audit trail
    private async Task LogAuditAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Models.Users user,
        Guid? activatedBy)
    {
        var auditLog = new AuditLogs
        {
            Id = Guid.NewGuid(),
            UserId = activatedBy ?? user.Id, // User thực hiện hành động (admin/manager)
            Action = "ACTIVATE_USER",
            EntityType = "User",
            EntityId = user.Id,
            OldValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                IsActive = false
            }),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new
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
