namespace Users.API.UsersHandler.SendLoginEmail;

/// <summary>
/// Handler để gửi email chứa thông tin đăng nhập
/// Tạo password tạm thời mới, hash và update vào DB, sau đó gửi email
/// </summary>
public class SendLoginEmailHandler(
    IDbConnectionFactory connectionFactory,
    EmailHandler emailHandler,
    ILogger<SendLoginEmailHandler> logger)
    : ICommandHandler<SendLoginEmailCommand, SendLoginEmailResult>
{
    public async Task<SendLoginEmailResult> Handle(
        SendLoginEmailCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Sending login email - Email: {Email}, Phone: {Phone}",
                request.Email,
                request.PhoneNumber);

            using var connection = await connectionFactory.CreateConnectionAsync();
            
            var query = @"
                SELECT * FROM users
                WHERE (Email = @Email OR Phone = @Phone)
                AND IsDeleted = 0
                LIMIT 1";

            var user = await connection.QueryFirstOrDefaultAsync<Models.Users>(
                query,
                new
                {
                    Email = request.Email,
                    Phone = request.PhoneNumber
                });

            if (user == null)
            {
                logger.LogWarning(
                    "User not found - Email: {Email}, Phone: {Phone}",
                    request.Email,
                    request.PhoneNumber);

                return new SendLoginEmailResult
                {
                    Success = false,
                    ErrorMessage = "User not found with provided email or phone number"
                };
            }

            logger.LogInformation(
                "User found - Id: {UserId}, Email: {Email}, FullName: {FullName}",
                user.Id,
                user.Email,
                user.FullName);

            // ================================================================
            // BƯỚC 2: TẠO PASSWORD TẠM THỜI MỚI
            // Password format: TEMP + 8 ký tự random (chữ hoa + số)
            // ================================================================
            var tempPassword = GenerateTemporaryPassword();

            logger.LogInformation(
                "Generated temporary password for user {UserId}",
                user.Id);

            // ================================================================
            // BƯỚC 3: HASH VÀ UPDATE PASSWORD VÀO DATABASE
            // ================================================================
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(tempPassword);
            user.Password = hashedPassword;
            user.UpdatedAt = DateTime.UtcNow;

            await connection.UpdateAsync(user);

            logger.LogInformation(
                "Password updated for user {UserId}",
                user.Id);

            // ================================================================
            // BƯỚC 4: GỬI EMAIL
            // ================================================================
            await emailHandler.SendLoginCredentialsEmailAsync(
                user.FullName,
                user.Email,
                tempPassword);

            logger.LogInformation(
                "Login credentials email sent successfully to {Email}",
                user.Email);

            return new SendLoginEmailResult
            {
                Success = true,
                Email = user.Email,
                FullName = user.FullName,
                EmailSent = true
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send login email - Email: {Email}, Phone: {Phone}",
                request.Email,
                request.PhoneNumber);

            return new SendLoginEmailResult
            {
                Success = false,
                ErrorMessage = $"Failed to send login email: {ex.Message}",
                EmailSent = false
            };
        }
    }

    /// <summary>
    /// Tạo password tạm thời: TEMP + 8 ký tự random
    /// Format: TEMPxxxx1234 (chữ hoa + số)
    /// </summary>
    private string GenerateTemporaryPassword()
    {
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "0123456789";
        const string special = "!@#$%^&*";
        const string allChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
    
        var random = new Random();
        var passwordChars = new List<char>();
        passwordChars.Add(uppercase[random.Next(uppercase.Length)]);
        passwordChars.Add(digits[random.Next(digits.Length)]);
        passwordChars.Add(special[random.Next(special.Length)]);
        for (int i = 0; i < 5; i++)
        {
            passwordChars.Add(allChars[random.Next(allChars.Length)]);
        }
        var shuffled = passwordChars.OrderBy(x => random.Next()).ToArray();
    
        return "TEMP" + new string(shuffled);
    }

}
