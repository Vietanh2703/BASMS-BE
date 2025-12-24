namespace Users.API.UsersHandler.CreateOtp;

public record CreateOtpCommand(
    string Email,
    string Purpose 
) : ICommand<CreateOtpResult>;

public record CreateOtpResult(
    Guid OtpId,
    string Message,
    DateTime ExpiresAt
);

internal class CreateOtpHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<CreateOtpHandler> logger,
    EmailHandler emailHandler,
    CreateOtpValidator validator)
    : ICommandHandler<CreateOtpCommand, CreateOtpResult>
{
    private const int OTP_LENGTH = 6;
    private const int OTP_EXPIRY_MINUTES = 10;
    private const int MAX_ATTEMPTS = 5;
    private const string OTP_CHARACTERS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public async Task<CreateOtpResult> Handle(CreateOtpCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Validation failed: {errors}");
        }

        try
        {
            logger.LogInformation("Creating OTP for email: {Email}, Purpose: {Purpose}", 
                command.Email, command.Purpose);

            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var users = await connection.GetAllAsync<Models.Users>(transaction);
                var user = users.FirstOrDefault(u => u.Email == command.Email && !u.IsDeleted);

                if (user == null)
                {
                    logger.LogWarning("User not found with email: {Email}", command.Email);
                    throw new InvalidOperationException($"User with email {command.Email} not found");
                }
                
                var existingOtps = await connection.GetAllAsync<OTPLogs>(transaction);
                var activeOtps = existingOtps.Where(o => 
                    o.UserId == user.Id && 
                    o.Purpose == command.Purpose && 
                    !o.IsUsed && 
                    !o.IsExpired &&
                    o.ExpiresAt > DateTime.UtcNow).ToList();

                foreach (var oldOtp in activeOtps)
                {
                    oldOtp.IsExpired = true;
                    oldOtp.UpdatedAt = DateTime.UtcNow;
                    await connection.UpdateAsync(oldOtp, transaction);
                }

                logger.LogDebug("Invalidated {Count} previous OTPs", activeOtps.Count);
                
                var otpCode = GenerateOtpCode();
                var expiresAt = DateTime.UtcNow.AddMinutes(OTP_EXPIRY_MINUTES);

                // Step 4: Create new OTP
                var newOtp = new OTPLogs
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    OtpCode = otpCode,
                    Purpose = command.Purpose,
                    AttemptCount = 0,
                    IsUsed = false,
                    IsExpired = false,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(OTP_EXPIRY_MINUTES),
                    DeliveryMethod = "email", 
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await connection.InsertAsync(newOtp, transaction);
                logger.LogDebug("OTP created in database: {OtpId}", newOtp.Id);

                transaction.Commit();
                
                try
                {
                    await emailHandler.SendOtpEmailAsync(user.FullName, user.Email, otpCode, command.Purpose, OTP_EXPIRY_MINUTES);
                    logger.LogInformation("OTP sent successfully to {Email}", user.Email);
                }
                catch (Exception emailEx)
                {
                    logger.LogError(emailEx, "Failed to send OTP email to {Email}, but OTP was created", user.Email);
                    throw new InvalidOperationException("Failed to send OTP email. Please try again.", emailEx);
                }

                return new CreateOtpResult(
                    newOtp.Id,
                    $"OTP sent successfully to {MaskEmail(user.Email)}",
                    expiresAt
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
            logger.LogError(ex, "Error creating OTP for email: {Email}", command.Email);
            throw;
        }
    }

    private string GenerateOtpCode()
    {
        var random = new Random();
        var otp = new char[OTP_LENGTH];
        
        for (int i = 0; i < OTP_LENGTH; i++)
        {
            otp[i] = OTP_CHARACTERS[random.Next(OTP_CHARACTERS.Length)];
        }
        
        var otpCode = new string(otp);
        logger.LogDebug("Generated OTP: {OTP}", otpCode);
        return otpCode;
    }

    private string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return email;

        var username = parts[0];
        var domain = parts[1];

        if (username.Length <= 2)
            return $"{username}***@{domain}";

        var maskedUsername = $"{username[0]}***{username[^1]}";
        return $"{maskedUsername}@{domain}";
    }
}