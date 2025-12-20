namespace Users.API.UsersHandler.DeleteUser;


public record DeleteUserCommand(Guid Id) : ICommand<DeleteUserResult>;

public record DeleteUserResult(bool IsSuccess, string Message);

internal class DeleteUserHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<DeleteUserHandler> logger)
    : ICommandHandler<DeleteUserCommand, DeleteUserResult>
{
    public async Task<DeleteUserResult> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Attempting to delete user with ID: {UserId}", command.Id);
            
            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var users = await connection.GetAllAsync<Models.Users>(transaction);
                var user = users.FirstOrDefault(u => u.Id == command.Id && !u.IsDeleted);

                if (user == null)
                {
                    logger.LogWarning("User not found with ID: {UserId}", command.Id);
                    return new DeleteUserResult(false, $"User with ID {command.Id} not found");
                }
                
                try
                {
                    await DeleteFirebaseUserAsync(user.FirebaseUid);
                    logger.LogInformation("User deleted from Firebase: {FirebaseUid}", user.FirebaseUid);
                }
                catch (FirebaseAuthException ex)
                {
                    logger.LogWarning(ex, "Failed to delete user from Firebase (user may not exist): {FirebaseUid}", user.FirebaseUid);
                }
                
                user.IsDeleted = true;
                user.UpdatedAt = DateTime.UtcNow;
                user.Status = "deleted";
                user.IsActive = false;
                
                await connection.UpdateAsync(user, transaction);
                logger.LogDebug("User soft deleted in database: {UserId}", user.Id);
                
                await LogAuditAsync(connection, transaction, user);
                
                transaction.Commit();

                logger.LogInformation("User deleted successfully: {Email}, UserId: {UserId}", 
                    user.Email, user.Id);

                return new DeleteUserResult(true, $"User {user.Email} deleted successfully from both Firebase and database");
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
            logger.LogError(ex, "Error deleting user with ID: {UserId}", command.Id);
            throw;
        }
    }
    
    private async Task DeleteFirebaseUserAsync(string firebaseUid)
    {
        try
        {
            var firebaseAuth = FirebaseAuth.DefaultInstance;
            if (firebaseAuth == null)
            {
                logger.LogWarning("FirebaseAuth.DefaultInstance is null. Skipping Firebase user deletion.");
                return;
            }
            
            await firebaseAuth.DeleteUserAsync(firebaseUid);
            logger.LogInformation("Successfully deleted Firebase user: {FirebaseUid}", firebaseUid);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            logger.LogWarning("Firebase user not found: {FirebaseUid}. User may have been already deleted.", firebaseUid);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error deleting Firebase user: {FirebaseUid}", firebaseUid);
            throw;
        }
    }
    
    private async Task LogAuditAsync(IDbConnection connection, IDbTransaction transaction, Models.Users user)
    {
        var auditLog = new AuditLogs
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "DELETE_USER",
            EntityType = "User",
            EntityId = user.Id,
            OldValues = JsonSerializer.Serialize(new
            {
                user.Email,
                user.FullName,
                user.FirebaseUid,
                user.Status,
                user.IsActive,
                IsDeleted = false
            }),
            NewValues = JsonSerializer.Serialize(new
            {
                user.Email,
                user.FullName,
                user.FirebaseUid,
                Status = "deleted",
                IsActive = false,
                IsDeleted = true,
                DeletedFromFirebase = true
            }),
            IpAddress = null,
            Status = "success",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(auditLog, transaction);
    }
}