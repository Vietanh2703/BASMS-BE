using BuildingBlocks.CQRS;
using Dapper.Contrib.Extensions;
using MediatR;
using Users.API.Data;
using Users.API.Models;
using Users.API.UsersHandler.CreateOtp;

namespace Users.API.UsersHandler.UpdatePassword;

public record UpdatePasswordCommand(
    string Email,
    string OldPassword,
    string NewPassword,
    string RetypePassword
) : ICommand<UpdatePasswordResult>;

public record UpdatePasswordResult(
    bool Success,
    string Message,
    DateTime OtpExpiresAt
);

internal class UpdatePasswordHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<UpdatePasswordHandler> logger,
    UpdatePasswordValidator validator,
    ISender sender)
    : ICommandHandler<UpdatePasswordCommand, UpdatePasswordResult>
{
    private const int OTP_EXPIRY_MINUTES = 10;

    public async Task<UpdatePasswordResult> Handle(UpdatePasswordCommand command, CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Validation failed: {errors}");
        }

        logger.LogInformation("Initiating password update for email: {Email}", command.Email);

        Guid userId;
        
        // Transaction scope - only for database operations
        using (var connection = await connectionFactory.CreateConnectionAsync())
        using (var transaction = connection.BeginTransaction())
        {
            try
            {
                // Step 1: Find user by email
                var users = await connection.GetAllAsync<Models.Users>(transaction);
                var user = users.FirstOrDefault(u => u.Email == command.Email && !u.IsDeleted);

                if (user == null)
                {
                    logger.LogWarning("User not found with email: {Email}", command.Email);
                    throw new InvalidOperationException("User not found");
                }

                userId = user.Id;

                // Step 2: Verify old password
                if (!BCrypt.Net.BCrypt.Verify(command.OldPassword, user.Password))
                {
                    logger.LogWarning("Invalid old password for user: {Email}", command.Email);
                    throw new InvalidOperationException("Current password is incorrect");
                }

                // Step 3: Check if new password is same as old password
                if (BCrypt.Net.BCrypt.Verify(command.NewPassword, user.Password))
                {
                    throw new InvalidOperationException("New password must be different from current password");
                }

                // Step 4: Invalidate previous password reset tokens
                var existingTokens = await connection.GetAllAsync<PasswordResetTokens>(transaction);
                var activeTokens = existingTokens.Where(t =>
                    t.UserId == user.Id &&
                    !t.IsUsed &&
                    !t.IsDeleted &&
                    t.ExpiresAt > DateTime.UtcNow).ToList();

                foreach (var oldToken in activeTokens)
                {
                    oldToken.IsDeleted = true;
                    oldToken.UpdatedAt = DateTime.UtcNow;
                    await connection.UpdateAsync(oldToken, transaction);
                }

                // Step 5: Create pending password reset entry
                var resetToken = new PasswordResetTokens
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Token = BCrypt.Net.BCrypt.HashPassword(command.NewPassword), // Store hashed new password
                    ExpiresAt = DateTime.UtcNow.AddMinutes(OTP_EXPIRY_MINUTES),
                    IsUsed = false,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await connection.InsertAsync(resetToken, transaction);
                logger.LogDebug("Password reset token created: {TokenId}", resetToken.Id);

                transaction.Commit();
                logger.LogInformation("Password reset token committed for user: {UserId}", userId);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                logger.LogError(ex, "Error in database transaction for password update: {Email}", command.Email);
                throw;
            }
        } // Transaction disposed here

        // Step 6: Create OTP via CreateOtpHandler (outside transaction scope)
        try
        {
            var createOtpCommand = new CreateOtpCommand(
                Email: command.Email,
                Purpose: "reset_password"
            );

            var otpResult = await sender.Send(createOtpCommand, cancellationToken);
            logger.LogInformation("Password change OTP created: {OtpId}", otpResult.OtpId);

            return new UpdatePasswordResult(
                true,
                "OTP has been sent to your email. Please verify to complete password change.",
                otpResult.ExpiresAt
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating OTP for password change: {Email}", command.Email);
            throw new InvalidOperationException("Password reset token created but failed to send OTP. Please try again.", ex);
        }
    }
}