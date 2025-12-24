namespace Users.API.UsersHandler.ConfirmPasswordChange;

public record ConfirmPasswordChangeCommand(
    string Email,
    string OtpCode
) : ICommand<ConfirmPasswordChangeResult>;

public record ConfirmPasswordChangeResult(
    bool Success,
    string Message
);

internal class ConfirmPasswordChangeHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ConfirmPasswordChangeHandler> logger,
    ConfirmPasswordChangeValidator validator,
    ISender sender)
    : ICommandHandler<ConfirmPasswordChangeCommand, ConfirmPasswordChangeResult>
{
    public async Task<ConfirmPasswordChangeResult> Handle(ConfirmPasswordChangeCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Validation failed: {errors}");
        }

        try
        {
            logger.LogInformation("Confirming password change for email: {Email}", command.Email);

            var verifyOtpCommand = new VerifyOtpCommand(
                Email: command.Email,
                OtpCode: command.OtpCode,
                Purpose: "reset_password"
            );

            var verifyResult = await sender.Send(verifyOtpCommand, cancellationToken);

            if (!verifyResult.IsValid)
            {
                logger.LogWarning("OTP verification failed for password change: {Email}", command.Email);
                return new ConfirmPasswordChangeResult(false, verifyResult.Message);
            }
            
            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var users = await connection.GetAllAsync<Models.Users>(transaction);
                var user = users.FirstOrDefault(u => u.Email == command.Email && !u.IsDeleted);

                if (user == null)
                {
                    throw new InvalidOperationException("User not found");
                }
                
                var resetTokens = await connection.GetAllAsync<PasswordResetTokens>(transaction);
                var pendingReset = resetTokens
                    .Where(t =>
                        t.UserId == user.Id &&
                        !t.IsUsed &&
                        !t.IsDeleted &&
                        t.ExpiresAt > DateTime.UtcNow)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefault();

                if (pendingReset == null)
                {
                    throw new InvalidOperationException("Password reset session has expired. Please request a new password change.");
                }
                
                user.Password = pendingReset.Token; 
                user.UpdatedAt = DateTime.UtcNow;
                await connection.UpdateAsync(user, transaction);
                
                pendingReset.IsUsed = true;
                pendingReset.UsedAt = DateTime.UtcNow;
                pendingReset.UpdatedAt = DateTime.UtcNow;
                await connection.UpdateAsync(pendingReset, transaction);
                
                await LogAuditAsync(connection, transaction, user.Id, "PASSWORD_CHANGED", 
                    "Password changed successfully via OTP confirmation");

                transaction.Commit();

                logger.LogInformation("Password changed successfully for user: {Email}", command.Email);
                return new ConfirmPasswordChangeResult(true, 
                    "Password changed successfully. Please login with your new password.");
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
            logger.LogError(ex, "Error confirming password change for email: {Email}", command.Email);
            throw;
        }
    }

    private async Task LogAuditAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string action,
        string message)
    {
        var auditLog = new AuditLogs
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityType = "User",
            EntityId = userId,
            NewValues = JsonSerializer.Serialize(new { Message = message }),
            Status = "success",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(auditLog, transaction);
    }
}
