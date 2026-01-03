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
        logger.LogInformation("Attempting to delete user with ID: {UserId}", command.Id);

        try
        {
            // Sử dụng TransactionHelper để giảm boilerplate code
            return await connectionFactory.ExecuteInTransactionAsync(
                async (connection, transaction) =>
                {
                    // Sử dụng DatabaseHelpers.GetUserByIdOrThrowAsync thay vì GetAllAsync + FirstOrDefault
                    var user = await connection.GetUserByIdOrThrowAsync(command.Id, transaction);

                    // Delete from Firebase (không cần try-catch riêng, Firebase helper sẽ handle)
                    await DeleteFirebaseUserAsync(user.FirebaseUid);

                    // Sử dụng DatabaseHelpers.SoftDeleteUserAsync để giảm code lặp lại
                    await connection.SoftDeleteUserAsync(user, transaction);
                    logger.LogDebug("User soft deleted in database: {UserId}", user.Id);

                    // Sử dụng AuditLogHelper.LogUserDeletedAsync
                    await connection.LogUserDeletedAsync(transaction, user);

                    logger.LogInformation("User deleted successfully: {Email}, UserId: {UserId}",
                        user.Email, user.Id);

                    return new DeleteUserResult(
                        true,
                        $"User {user.Email} deleted successfully from both Firebase and database");
                },
                logger,
                "DeleteUser");
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
}