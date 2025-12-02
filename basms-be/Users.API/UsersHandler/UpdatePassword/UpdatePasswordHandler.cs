using BuildingBlocks.Exceptions;

namespace Users.API.UsersHandler.UpdatePassword;

public record UpdatePasswordCommand(
    string Email,
    string NewPassword,
    string ConfirmPassword
) : ICommand<UpdatePasswordResult>;

public record UpdatePasswordResult(
    bool Success,
    string Message,
    int NewLoginCount
);

internal class UpdatePasswordHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<UpdatePasswordHandler> logger,
    UpdatePasswordValidator validator)
    : ICommandHandler<UpdatePasswordCommand, UpdatePasswordResult>
{
    public async Task<UpdatePasswordResult> Handle(UpdatePasswordCommand command, CancellationToken cancellationToken)
    {
        // Validate input
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var validationErrors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );
            throw new BuildingBlocks.Exceptions.ValidationException(validationErrors, "UPDATE_PASSWORD_VALIDATION_ERROR");
        }

        logger.LogInformation("Updating password for email: {Email}", command.Email);

        using var connection = await connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Tìm user theo email
            var users = await connection.GetAllAsync<Models.Users>(transaction);
            var user = users.FirstOrDefault(u => u.Email == command.Email && !u.IsDeleted);

            if (user == null)
            {
                logger.LogWarning("User not found with email: {Email}", command.Email);
                throw new NotFoundException("User not found", "USER_NOT_FOUND");
            }

            // Kiểm tra password mới khác password cũ
            if (BCrypt.Net.BCrypt.Verify(command.NewPassword, user.Password))
            {
                throw new BadRequestException(
                    "New password must be different from current password",
                    "PASSWORD_SAME_AS_OLD");
            }

            // Cập nhật password
            user.Password = BCrypt.Net.BCrypt.HashPassword(command.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            user.LoginCount += 1;

            await connection.UpdateAsync(user, transaction);

            logger.LogInformation(
                "Password updated for user: {UserId}. LoginCount increased to {LoginCount}",
                user.Id,
                user.LoginCount);

            // Vô hiệu hóa các password reset tokens cũ (nếu có)
            var existingTokens = await connection.GetAllAsync<PasswordResetTokens>(transaction);
            var activeTokens = existingTokens.Where(t =>
                t.UserId == user.Id &&
                !t.IsUsed &&
                !t.IsDeleted).ToList();

            foreach (var token in activeTokens)
            {
                token.IsDeleted = true;
                token.UpdatedAt = DateTime.UtcNow;
                await connection.UpdateAsync(token, transaction);
            }

            transaction.Commit();

            logger.LogInformation("Password updated successfully for user: {UserId}", user.Id);

            return new UpdatePasswordResult(
                Success: true,
                Message: "Password updated successfully",
                NewLoginCount: user.LoginCount
            );
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            logger.LogError(ex, "Error updating password for email: {Email}", command.Email);
            throw;
        }
    }
}
