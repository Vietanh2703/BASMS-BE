namespace Chats.API.ChatHandler.SendMessage;

/// <summary>
/// Endpoint để gửi tin nhắn text
/// </summary>
public class SendMessageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chats/messages", async (
            HttpContext httpContext,
            ISender sender,
            ILogger<SendMessageEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("POST /api/chats/messages - Sending message");

            // Get userId from JWT claims
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? httpContext.User.FindFirst("sub")?.Value
                      ?? httpContext.User.FindFirst("userId")?.Value;

            if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var senderGuid))
            {
                logger.LogWarning("Unauthorized: Invalid or missing userId in token");
                return Results.Unauthorized();
            }

            // Parse request body
            var request = await httpContext.Request.ReadFromJsonAsync<SendMessageRequest>(cancellationToken);

            if (request == null)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    error = "Invalid request body"
                });
            }

            // Create command
            var command = new SendMessageCommand(
                ConversationId: request.ConversationId,
                SenderId: senderGuid,
                Content: request.Content,
                ReplyToMessageId: request.ReplyToMessageId
            );

            // Execute
            var result = await sender.Send(command, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning("Failed to send message: {Error}", result.ErrorMessage);
                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "✓ Message {MessageId} sent successfully by user {UserId}",
                result.MessageId, senderGuid);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    messageId = result.MessageId,
                    createdAt = result.CreatedAt,
                    message = result.Message
                },
                message = "Message sent successfully"
            });
        })
        .RequireAuthorization()
        .WithName("SendMessage")
        .WithTags("Chats - Messages")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .WithSummary("Send a text message")
        .WithDescription(@"
            Send a text message to a conversation.

            Features:
            - Validates user is participant of conversation
            - Saves message to database
            - Updates conversation last message preview
            - Broadcasts to all users in conversation via SignalR
            - Supports replying to another message

            Request Body:
            {
                ""conversationId"": ""guid"",
                ""content"": ""message text (max 10000 chars)"",
                ""replyToMessageId"": ""guid (optional)""
            }

            Real-time:
            - All users in the conversation will receive the message via SignalR
            - Method: ReceiveMessage(messageDto)
        ");
    }
}

/// <summary>
/// Request DTO cho SendMessage endpoint
/// </summary>
public record SendMessageRequest
{
    public Guid ConversationId { get; init; }
    public string Content { get; init; } = string.Empty;
    public Guid? ReplyToMessageId { get; init; }
}
