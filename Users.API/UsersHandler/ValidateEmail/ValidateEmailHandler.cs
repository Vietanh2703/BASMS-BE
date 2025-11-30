namespace Users.API.UsersHandler.ValidateEmail;

// Command để validate email có tồn tại trong hệ thống hay không
public record ValidateEmailCommand(
    string Email
) : ICommand<ValidateEmailResult>;

public record ValidateEmailResult(
    bool IsValid,
    string Message,
    Guid? UserId = null,
    string? FullName = null
);

internal class ValidateEmailHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ValidateEmailHandler> logger,
    ValidateEmailValidator validator)
    : ICommandHandler<ValidateEmailCommand, ValidateEmailResult>
{
    public async Task<ValidateEmailResult> Handle(ValidateEmailCommand command, CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new ValidationException($"Validation failed: {errors}");
        }

        try
        {
            logger.LogInformation("Validating email: {Email}", command.Email);

            using var connection = await connectionFactory.CreateConnectionAsync();
            var users = await connection.GetAllAsync<Models.Users>();
            var user = users.FirstOrDefault(u => u.Email == command.Email && !u.IsDeleted);

            if (user == null)
            {
                logger.LogWarning("Email not found: {Email}", command.Email);
                return new ValidateEmailResult(
                    false,
                    "Email address not found in our system."
                );
            }

            // Check if account is active
            if (!user.IsActive)
            {
                logger.LogWarning("Email found but account is inactive: {Email}", command.Email);
                return new ValidateEmailResult(
                    false,
                    "Your account is inactive. Please contact support."
                );
            }

            logger.LogInformation("Email validated successfully: {Email}", command.Email);

            return new ValidateEmailResult(
                true,
                "Email address is valid and active.",
                user.Id,
                user.FullName
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating email: {Email}", command.Email);
            throw;
        }
    }
}
