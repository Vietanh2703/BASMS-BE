namespace Chats.API.ChatHandler.EditMessage;

/// <summary>
/// Endpoint để chỉnh sửa tin nhắn
/// </summary>
public class EditMessageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/chats/messages/{messageId}", async (
            Guid messageId,
            HttpContext httpContext,
            ISender sender,
            ILogger<EditMessageEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("PUT /api/chats/messages/{MessageId} - Editing message", messageId);

            // Get userId from JWT claims
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? httpContext.User.FindFirst("sub")?.Value
                      ?? httpContext.User.FindFirst("userId")?.Value;

            if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                logger.LogWarning("Unauthorized: Invalid or missing userId in token");
                return Results.Unauthorized();
            }

            // Parse request body
            var request = await httpContext.Request.ReadFromJsonAsync<EditMessageRequest>(cancellationToken);

            if (request == null)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    error = "Invalid request body"
                });
            }

            // Create command
            var command = new EditMessageCommand(
                MessageId: messageId,
                UserId: userGuid,
                NewContent: request.Content
            );

            // Execute
            var result = await sender.Send(command, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning("Failed to edit message: {Error}", result.ErrorMessage);
                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "✓ Message {MessageId} edited successfully by user {UserId}",
                messageId, userGuid);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    messageId,
                    editedAt = result.EditedAt
                },
                message = "Message edited successfully"
            });
        })
        .RequireAuthorization()
        .WithName("EditMessage")
        .WithTags("Chats - Messages")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .WithSummary("Edit a message")
        .WithDescription(@"
            Edit a text message.

            Features:
            - Only sender can edit their own messages
            - Only TEXT messages can be edited
            - Cannot edit deleted messages
            - Updates conversation preview if this is the last message
            - Broadcasts edit notification to all users in conversation

            Request Body:
            {
                ""content"": ""new message text (max 10000 chars)""
            }

            Real-time:
            - All users in conversation receive edit notification via SignalR
            - Method: MessageEdited(messageId, newContent, editedAt)
        ");
    }
}

/// <summary>
/// Request DTO cho EditMessage endpoint
/// </summary>
public record EditMessageRequest
{
    public string Content { get; init; } = string.Empty;
}
