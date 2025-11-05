namespace Users.API.UsersHandler.UpdateOtp;

public record UpdateOtpCommand(
    string Email,
    string Purpose
) : ICommand<UpdateOtpResult>;

public record UpdateOtpResult(
    bool Success,
    string Message,
    DateTime? ExpiredAt = null,
    int? AttemptMax = null
);

internal class UpdateOtpHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<UpdateOtpHandler> logger,
    UpdateOtpValidator validator)
    : ICommandHandler<UpdateOtpCommand, UpdateOtpResult>
{
    private const int MAX_ATTEMPTS = 5;
    private const int OTP_EXPIRY_MINUTES = 10;
    private const int OTP_LENGTH = 6;

    public async Task<UpdateOtpResult> Handle(UpdateOtpCommand command, CancellationToken cancellationToken)
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
            logger.LogInformation("Refreshing OTP for email: {Email}, Purpose: {Purpose}", 
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
                    return new UpdateOtpResult(false, "User not found");
                }

                // Step 2: Find active OTP
                var otpLogs = await connection.GetAllAsync<OTPLogs>(transaction);
                var activeOtp = otpLogs
                    .Where(o => 
                        o.UserId == user.Id && 
                        o.Purpose == command.Purpose && 
                        !o.IsUsed && 
                        !o.IsDeleted)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefault();

                if (activeOtp == null)
                {
                    logger.LogWarning("No active OTP found for user: {Email}", command.Email);
                    return new UpdateOtpResult(false, "No OTP found for this email and purpose");
                }

                // Step 3: Generate new OTP code
                var newOtpCode = GenerateOtpCode();

                // Step 4: Refresh OTP - reset attempt_max, gia hạn expired_at thêm 10 phút và thay đổi OTP code
                activeOtp.OtpCode = newOtpCode;
                activeOtp.AttemptCount = 0;
                activeOtp.ExpiresAt = DateTime.UtcNow.AddMinutes(OTP_EXPIRY_MINUTES);
                activeOtp.IsExpired = false;
                activeOtp.UpdatedAt = DateTime.UtcNow;

                await connection.UpdateAsync(activeOtp, transaction);

                // Step 5: Log audit
                await LogAuditAsync(connection, transaction, user.Id, "OTP_REFRESHED", 
                    $"OTP refreshed for purpose: {command.Purpose}. New expiry: {activeOtp.ExpiresAt}");

                transaction.Commit();

                logger.LogInformation("OTP refreshed successfully for user: {Email} with new code", command.Email);
                return new UpdateOtpResult(
                    true, 
                    "OTP refreshed successfully. A new code has been generated. You have 10 more minutes and attempts reset to maximum.",
                    activeOtp.ExpiresAt,
                    MAX_ATTEMPTS
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
            logger.LogError(ex, "Error refreshing OTP for email: {Email}", command.Email);
            throw;
        }
    }

    private string GenerateOtpCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var otpCode = new char[OTP_LENGTH];
        
        for (int i = 0; i < OTP_LENGTH; i++)
        {
            otpCode[i] = chars[random.Next(chars.Length)];
        }
        
        return new string(otpCode);
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