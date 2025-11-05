// Handler xử lý logic đổi mật khẩu
// Thực hiện: Verify old password -> Tạo pending password reset -> Gửi OTP -> Chờ verify OTP để confirm
namespace Users.API.UsersHandler.UpdatePassword;

// Command để đổi password
// Yêu cầu verify old password trước khi cho phép đổi
public record UpdatePasswordCommand(
    string Email,           // Email của user
    string OldPassword,     // Password hiện tại (để verify)
    string NewPassword,     // Password mới
    string RetypePassword   // Nhập lại password mới (để confirm)
) : ICommand<UpdatePasswordResult>;

// Result trả về
// Chứa OTP expiry time để user biết thời gian hết hạn
public record UpdatePasswordResult(
    bool Success,
    string Message,
    DateTime OtpExpiresAt   // Thời điểm OTP hết hạn
);

internal class UpdatePasswordHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<UpdatePasswordHandler> logger,
    UpdatePasswordValidator validator,
    ISender sender)                         // MediatR sender để gọi CreateOtpHandler
    : ICommandHandler<UpdatePasswordCommand, UpdatePasswordResult>
{
    // OTP hết hạn sau 10 phút
    private const int OTP_EXPIRY_MINUTES = 10;

    public async Task<UpdatePasswordResult> Handle(UpdatePasswordCommand command, CancellationToken cancellationToken)
    {
        // Bước 1: Validate command input
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Validation failed: {errors}");
        }

        logger.LogInformation("Initiating password update for email: {Email}", command.Email);

        Guid userId;
        
        // Transaction scope - chỉ cho database operations
        using (var connection = await connectionFactory.CreateConnectionAsync())
        using (var transaction = connection.BeginTransaction())
        {
            try
            {
                // Bước 2: Tìm user theo email
                var users = await connection.GetAllAsync<Models.Users>(transaction);
                var user = users.FirstOrDefault(u => u.Email == command.Email && !u.IsDeleted);

                if (user == null)
                {
                    logger.LogWarning("User not found with email: {Email}", command.Email);
                    throw new InvalidOperationException("User not found");
                }

                userId = user.Id;

                // Bước 3: Verify old password
                // So sánh old password với password đã hash trong database
                if (!BCrypt.Net.BCrypt.Verify(command.OldPassword, user.Password))
                {
                    logger.LogWarning("Invalid old password for user: {Email}", command.Email);
                    throw new InvalidOperationException("Current password is incorrect");
                }

                // Bước 4: Kiểm tra new password khác old password
                // Không cho phép đổi sang password giống cũ
                if (BCrypt.Net.BCrypt.Verify(command.NewPassword, user.Password))
                {
                    throw new InvalidOperationException("New password must be different from current password");
                }

                // Bước 5: Vô hiệu hóa các password reset tokens cũ
                // XÓA tất cả tokens đang active để tránh conflict
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

                // Bước 6: Tạo pending password reset entry
                // Lưu password mới (đã hash) vào bảng password_reset_tokens
                // Password chưa được apply, chờ verify OTP
                var resetToken = new PasswordResetTokens
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Token = BCrypt.Net.BCrypt.HashPassword(command.NewPassword), // Hash password mới
                    ExpiresAt = DateTime.UtcNow.AddMinutes(OTP_EXPIRY_MINUTES),
                    IsUsed = false,     // Chưa sử dụng
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await connection.InsertAsync(resetToken, transaction);
                logger.LogDebug("Password reset token created: {TokenId}", resetToken.Id);

                // Commit transaction
                transaction.Commit();
                logger.LogInformation("Password reset token committed for user: {UserId}", userId);
            }
            catch (Exception ex)
            {
                // Rollback nếu có lỗi
                transaction.Rollback();
                logger.LogError(ex, "Error in database transaction for password update: {Email}", command.Email);
                throw;
            }
        } // Transaction disposed ở đây

        // Bước 7: Tạo OTP qua CreateOtpHandler (ngoài transaction scope)
        // Gọi CreateOtpHandler thông qua MediatR để gửi OTP qua email
        try
        {
            var createOtpCommand = new CreateOtpCommand(
                Email: command.Email,
                Purpose: "reset_password"   // Mục đích: reset password
            );

            // Gửi command đến CreateOtpHandler
            var otpResult = await sender.Send(createOtpCommand, cancellationToken);
            logger.LogInformation("Password change OTP created: {OtpId}", otpResult.OtpId);

            // Trả về kết quả với OTP expiry time
            return new UpdatePasswordResult(
                true,
                "OTP has been sent to your email. Please verify to complete password change.",
                otpResult.ExpiresAt
            );
        }
        catch (Exception ex)
        {
            // Nếu gửi OTP thất bại, password reset token vẫn được tạo
            // User có thể thử lại sau
            logger.LogError(ex, "Error creating OTP for password change: {Email}", command.Email);
            throw new InvalidOperationException("Password reset token created but failed to send OTP. Please try again.", ex);
        }
    }
}