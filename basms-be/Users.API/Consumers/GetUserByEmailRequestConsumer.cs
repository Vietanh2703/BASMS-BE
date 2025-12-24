using BuildingBlocks.Messaging.Events;

namespace Users.API.Consumers;

public class GetUserByEmailRequestConsumer(
    IDbConnectionFactory connectionFactory,
    ILogger<GetUserByEmailRequestConsumer> logger)
    : IConsumer<GetUserByEmailRequest>
{
    public async Task Consume(ConsumeContext<GetUserByEmailRequest> context)
    {
        var request = context.Message;

        logger.LogInformation(
            "Received GetUserByEmailRequest for email: {Email}",
            request.Email);

        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                logger.LogWarning("Email is empty in GetUserByEmailRequest");

                await context.RespondAsync(new GetUserByEmailResponse
                {
                    Success = false,
                    ErrorMessage = "Email is required",
                    UserExists = false
                });
                return;
            }
            
            using var connection = await connectionFactory.CreateConnectionAsync();

            var sql = @"
                SELECT * FROM users
                WHERE Email = @Email
                AND IsDeleted = 0
                LIMIT 1";

            var user = await connection.QueryFirstOrDefaultAsync<Models.Users>(
                sql,
                new { Email = request.Email });

            if (user == null)
            {
                logger.LogInformation("User not found for email: {Email}", request.Email);

                await context.RespondAsync(new GetUserByEmailResponse
                {
                    Success = true,
                    UserExists = false,
                    Email = request.Email
                });
                return;
            }

            logger.LogInformation(
                "User found: {UserId} for email: {Email}",
                user.Id, user.Email);
            
            await context.RespondAsync(new GetUserByEmailResponse
            {
                Success = true,
                UserExists = true,
                UserId = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user by email: {Email}", request.Email);

            await context.RespondAsync(new GetUserByEmailResponse
            {
                Success = false,
                ErrorMessage = $"Error: {ex.Message}",
                UserExists = false,
                Email = request.Email
            });
        }
    }
}
