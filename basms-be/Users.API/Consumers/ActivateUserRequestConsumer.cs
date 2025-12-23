using ActivateUserRequest = BuildingBlocks.Messaging.Events.ActivateUserRequest;
using ActivateUserResponse = BuildingBlocks.Messaging.Events.ActivateUserResponse;
using Users.API.UsersHandler.ActivateUser;

namespace Users.API.Consumers;

public class ActivateUserRequestConsumer(
    ISender sender,
    ILogger<ActivateUserRequestConsumer> logger)
    : IConsumer<ActivateUserRequest>
{
    public async Task Consume(ConsumeContext<ActivateUserRequest> context)
    {
        var request = context.Message;

        logger.LogInformation(
            "Received ActivateUserRequest for UserId: {UserId}",
            request.UserId);

        try
        {
            // Gọi ActivateUserHandler để activate user
            var command = new ActivateUserCommand(
                UserId: request.UserId,
                ActivatedBy: request.ActivatedBy
            );

            var result = await sender.Send(command);

            // Trả về response
            await context.RespondAsync(new ActivateUserResponse
            {
                Success = result.Success,
                Message = result.Message,
                UserId = result.UserId,
                Email = result.Email,
                FullName = result.FullName
            });

            if (result.Success)
            {
                logger.LogInformation(
                    "Successfully activated user via request: UserId={UserId}, Email={Email}",
                    result.UserId,
                    result.Email);
            }
            else
            {
                logger.LogWarning(
                    "Failed to activate user via request: UserId={UserId}, Error={Error}",
                    request.UserId,
                    result.Message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error activating user via request: UserId={UserId}", request.UserId);

            await context.RespondAsync(new ActivateUserResponse
            {
                Success = false,
                Message = $"Unexpected error: {ex.Message}",
                UserId = request.UserId
            });
        }
    }
}
