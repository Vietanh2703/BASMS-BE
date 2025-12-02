using BuildingBlocks.Exceptions;

namespace Users.API.UsersHandler.CheckFirstLogin;

public record CheckFirstLoginQuery(string Email) : IQuery<CheckFirstLoginResult>;

public record CheckFirstLoginResult(
    bool IsFirstLogin,
    string Email,
    int LoginCount
);

internal class CheckFirstLoginHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<CheckFirstLoginHandler> logger)
    : IQueryHandler<CheckFirstLoginQuery, CheckFirstLoginResult>
{
    public async Task<CheckFirstLoginResult> Handle(CheckFirstLoginQuery query, CancellationToken cancellationToken)
    {
        logger.LogInformation("Checking first login for email: {Email}", query.Email);

        if (string.IsNullOrWhiteSpace(query.Email))
        {
            throw new BadRequestException("Email is required", "EMAIL_REQUIRED");
        }

        using var connection = await connectionFactory.CreateConnectionAsync();

        try
        {
            const string sql = """
                SELECT `Id`, `Email`, `LoginCount`, `IsDeleted`, `IsActive`
                FROM `users`
                WHERE `Email` = @Email
                LIMIT 1
            """;

            var user = await connection.QuerySingleOrDefaultAsync<Models.Users>(
                sql,
                new { Email = query.Email });

            if (user == null)
            {
                logger.LogWarning("User not found with email: {Email}", query.Email);
                throw new NotFoundException("User not found", "USER_NOT_FOUND");
            }

            if (user.IsDeleted)
            {
                logger.LogWarning("User is deleted: {Email}", query.Email);
                throw new BadRequestException("User account is deleted", "USER_DELETED");
            }

            if (!user.IsActive)
            {
                logger.LogWarning("User is inactive: {Email}", query.Email);
                throw new BadRequestException("User account is inactive", "USER_INACTIVE");
            }
            
            var isFirstLogin = user.LoginCount == 1;

            logger.LogInformation(
                "First login check result for {Email}: IsFirstLogin={IsFirstLogin}, LoginCount={LoginCount}",
                query.Email,
                isFirstLogin,
                user.LoginCount);

            return new CheckFirstLoginResult(
                IsFirstLogin: isFirstLogin,
                Email: user.Email,
                LoginCount: user.LoginCount
            );
        }
        catch (Exception ex) when (ex is not NotFoundException && ex is not BadRequestException)
        {
            logger.LogError(ex, "Error checking first login for email: {Email}", query.Email);
            throw;
        }
    }
}
