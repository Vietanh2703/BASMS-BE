namespace Users.API.UsersHandler.ResetPassword;

public record RequestResetPasswordCommand(
    string Email
) : ICommand<RequestResetPasswordResult>;

public record RequestResetPasswordResult(
    bool Success,
    string Message,
    DateTime? ExpiresAt = null
);

internal class RequestResetPasswordHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<RequestResetPasswordHandler> logger,
    ISender sender,
    RequestResetPasswordValidator validator) 
    : ICommandHandler<RequestResetPasswordCommand, RequestResetPasswordResult>
{
    public async Task<RequestResetPasswordResult> Handle(RequestResetPasswordCommand command, CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Validation failed: {errors}");
        }

        try
        {
            logger.LogInformation("Processing reset password request for email: {Email}", command.Email);
            
            using var connection = await connectionFactory.CreateConnectionAsync();
            var users = await connection.GetAllAsync<Models.Users>();
            var user = users.FirstOrDefault(u => u.Email == command.Email && !u.IsDeleted);

            if (user == null)
            {
                logger.LogWarning("Reset password requested for non-existent email: {Email}", command.Email);
                return new RequestResetPasswordResult(
                    false,
                    "Unable to process password reset request. Please verify your email address."
                );
            }


            if (!user.IsActive)
            {
                logger.LogWarning("Reset password requested for inactive account: {Email}", command.Email);
                return new RequestResetPasswordResult(
                    false,
                    "Your account is inactive. Please contact support."
                );
            }
            
            if (user.Status?.ToLower() == "locked")
            {
                logger.LogWarning("Reset password requested for locked account: {Email}", command.Email);
                return new RequestResetPasswordResult(
                    false,
                    "Your account has been locked. Please contact support to unlock your account."
                );
            }

            if (user.Status?.ToLower() == "suspended")
            {
                logger.LogWarning("Reset password requested for suspended account: {Email}", command.Email);
                return new RequestResetPasswordResult(
                    false,
                    "Your account has been suspended. Please contact support."
                );
            }
            
            var createOtpCommand = new CreateOtpCommand(
                Email: command.Email,
                Purpose: "reset_password"
            );

            var otpResult = await sender.Send(createOtpCommand, cancellationToken);

            logger.LogInformation("OTP sent successfully for password reset: {Email}", command.Email);

            return new RequestResetPasswordResult(
                true,
                otpResult.Message,
                otpResult.ExpiresAt
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing reset password request for email: {Email}", command.Email);
            throw;
        }
    }
}

public record VerifyResetPasswordOtpCommand(
    string Email,
    string OtpCode
) : ICommand<VerifyResetPasswordOtpResult>;

public record VerifyResetPasswordOtpResult(
    bool IsValid,
    string Message,
    Guid? UserId = null
);

internal class VerifyResetPasswordOtpHandler(
    ILogger<VerifyResetPasswordOtpHandler> logger,
    ISender sender,
    VerifyResetPasswordOtpValidator validator) 
    : ICommandHandler<VerifyResetPasswordOtpCommand, VerifyResetPasswordOtpResult>
{
    public async Task<VerifyResetPasswordOtpResult> Handle(VerifyResetPasswordOtpCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Validation failed: {errors}");
        }

        try
        {
            logger.LogInformation("Verifying OTP for password reset: {Email}", command.Email);
            
            var verifyOtpCommand = new VerifyOtpCommand(
                Email: command.Email,
                OtpCode: command.OtpCode,
                Purpose: "reset_password"
            );

            var verifyResult = await sender.Send(verifyOtpCommand, cancellationToken);

            if (verifyResult.IsValid)
            {
                logger.LogInformation("OTP verified successfully for password reset: {Email}", command.Email);
            }
            else
            {
                logger.LogWarning("OTP verification failed for password reset: {Email}", command.Email);
            }

            return new VerifyResetPasswordOtpResult(
                verifyResult.IsValid,
                verifyResult.Message,
                verifyResult.UserId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying OTP for password reset: {Email}", command.Email);
            throw;
        }
    }
}


public record CompleteResetPasswordCommand(
    string Email,
    string NewPassword,
    string ConfirmPassword
) : ICommand<CompleteResetPasswordResult>;

public record CompleteResetPasswordResult(
    bool Success,
    string Message
);

internal class CompleteResetPasswordHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<CompleteResetPasswordHandler> logger,
    CompleteResetPasswordValidator validator)
    : ICommandHandler<CompleteResetPasswordCommand, CompleteResetPasswordResult>
{
    public async Task<CompleteResetPasswordResult> Handle(CompleteResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Validation failed: {errors}");
        }

        try
        {
            logger.LogInformation("Processing password update for email: {Email}", command.Email);

            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var users = await connection.GetAllAsync<Models.Users>(transaction);
                var user = users.FirstOrDefault(u => u.Email == command.Email && !u.IsDeleted);

                if (user == null)
                {
                    logger.LogWarning("User not found for password reset: {Email}", command.Email);
                    return new CompleteResetPasswordResult(
                        false,
                        "User not found"
                    );
                }
                
                if (!user.IsActive)
                {
                    logger.LogWarning("Attempted password reset for inactive account: {Email}", command.Email);
                    return new CompleteResetPasswordResult(
                        false,
                        "Your account is inactive. Please contact support."
                    );
                }
                
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(command.NewPassword);
                user.Password = hashedPassword;
                user.UpdatedAt = DateTime.UtcNow;

                await connection.UpdateAsync(user, transaction);
                
                await LogAuditAsync(connection, transaction, user.Id, "PASSWORD_RESET",
                    "Password was reset successfully via OTP verification");

                transaction.Commit();

                logger.LogInformation("Password reset successfully for user: {Email}", command.Email);

                return new CompleteResetPasswordResult(
                    true,
                    "Password has been reset successfully. You can now log in with your new password."
                );
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
            logger.LogError(ex, "Error resetting password for email: {Email}", command.Email);
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