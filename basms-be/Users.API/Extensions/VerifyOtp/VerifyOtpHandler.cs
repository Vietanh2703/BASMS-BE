using BuildingBlocks.CQRS;
using Dapper.Contrib.Extensions;
using Users.API.Data;
using Users.API.Models;

namespace Users.API.UsersHandler.VerifyOtp;

public record VerifyOtpCommand(
    string Email,
    string OtpCode,
    string Purpose
) : ICommand<VerifyOtpResult>;

public record VerifyOtpResult(
    bool IsValid,
    string Message,
    Guid? UserId = null
);

internal class VerifyOtpHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<VerifyOtpHandler> logger,
    VerifyOtpValidator validator)
    : ICommandHandler<VerifyOtpCommand, VerifyOtpResult>
{
    private const int MAX_ATTEMPTS = 5;

    public async Task<VerifyOtpResult> Handle(VerifyOtpCommand command, CancellationToken cancellationToken)
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
            logger.LogInformation("Verifying OTP for email: {Email}, Purpose: {Purpose}", 
                command.Email, command.Purpose);

            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Step 1: Find user by email
                var users = await connection.GetAllAsync<Models.Users>(transaction);
                var user = users.FirstOrDefault(u => u.Email == command.Email && !u.IsDeleted);

                if (user == null)
                {
                    logger.LogWarning("User not found with email: {Email}", command.Email);
                    return new VerifyOtpResult(false, "User not found");
                }

                // Step 2: Check if user account is locked
                if (!user.IsActive || user.Status == "suspended" || user.Status == "locked")
                {
                    logger.LogWarning("User account is locked or suspended: {Email}", command.Email);
                    return new VerifyOtpResult(false, "Your account has been locked due to too many failed OTP attempts. Please contact support.");
                }

                // Step 3: Find active OTP
                var otpLogs = await connection.GetAllAsync<OTPLogs>(transaction);
                var activeOtp = otpLogs
                    .Where(o => 
                        o.UserId == user.Id && 
                        o.Purpose == command.Purpose && 
                        !o.IsUsed && 
                        !o.IsExpired &&
                        o.ExpiresAt > DateTime.UtcNow)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefault();

                if (activeOtp == null)
                {
                    logger.LogWarning("No active OTP found for user: {Email}", command.Email);
                    return new VerifyOtpResult(false, "OTP has expired or not found. Please request a new one.");
                }

                // Step 4: Increment attempt count
                activeOtp.AttemptCount++;
                activeOtp.LastAttemptAt = DateTime.UtcNow;
                activeOtp.UpdatedAt = DateTime.UtcNow;

                // Step 5: Check if max attempts exceeded
                if (activeOtp.AttemptCount >= MAX_ATTEMPTS)
                {
                    // Mark OTP as expired
                    activeOtp.IsExpired = true;
                    await connection.UpdateAsync(activeOtp, transaction);

                    // Lock user account
                    user.IsActive = false;
                    user.Status = "locked";
                    user.UpdatedAt = DateTime.UtcNow;
                    await connection.UpdateAsync(user, transaction);

                    // Log audit
                    await LogAuditAsync(connection, transaction, user.Id, "ACCOUNT_LOCKED", 
                        $"Account locked due to {MAX_ATTEMPTS} failed OTP attempts");

                    transaction.Commit();

                    logger.LogWarning("User account locked due to max OTP attempts: {Email}", command.Email);
                    return new VerifyOtpResult(false, 
                        $"Maximum OTP attempts ({MAX_ATTEMPTS}) exceeded. Your account has been locked for security. Please contact support.");
                }

                // Step 6: Verify OTP code
                if (activeOtp.OtpCode != command.OtpCode.ToUpper())
                {
                    await connection.UpdateAsync(activeOtp, transaction);
                    transaction.Commit();

                    var remainingAttempts = MAX_ATTEMPTS - activeOtp.AttemptCount;
                    logger.LogWarning("Invalid OTP attempt for user: {Email}, Attempts: {Attempts}/{MaxAttempts}", 
                        command.Email, activeOtp.AttemptCount, MAX_ATTEMPTS);
                    
                    return new VerifyOtpResult(false, 
                        $"Invalid OTP code. You have {remainingAttempts} attempt(s) remaining.");
                }

                // Step 7: OTP is valid - mark as used
                activeOtp.IsUsed = true;
                activeOtp.IsDeleted = true;
                activeOtp.UsedAt = DateTime.UtcNow;
                await connection.UpdateAsync(activeOtp, transaction);

                // Log successful verification
                await LogAuditAsync(connection, transaction, user.Id, "OTP_VERIFIED", 
                    $"OTP verified successfully for purpose: {command.Purpose}");

                transaction.Commit();

                logger.LogInformation("OTP verified successfully for user: {Email}", command.Email);
                return new VerifyOtpResult(true, "OTP verified successfully", user.Id);
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
            logger.LogError(ex, "Error verifying OTP for email: {Email}", command.Email);
            throw;
        }
    }

    private async Task LogAuditAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid userId,
        string action,
        string message)
    {
        var auditLog = new AuditLogs
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityType = "OTP",
            EntityId = userId,
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { Message = message }),
            Status = "success",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(auditLog, transaction);
    }
}
