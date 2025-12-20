namespace Chats.API.ChatHandler.DeleteMessage;

/// <summary>
/// Endpoint để xóa tin nhắn
/// </summary>
public class DeleteMessageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/chats/messages/{messageId}", async (
            Guid messageId,
            HttpContext httpContext,
            ISender sender,
            ILogger<DeleteMessageEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("DELETE /api/chats/messages/{MessageId} - Deleting message", messageId);

            // Get userId from JWT claims
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? httpContext.User.FindFirst("sub")?.Value
                      ?? httpContext.User.FindFirst("userId")?.Value;

            if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                logger.LogWarning("Unauthorized: Invalid or missing userId in token");
                return Results.Unauthorized();
            }

            // Create command
            var command = new DeleteMessageCommand(
                MessageId: messageId,
                UserId: userGuid
            );

            // Execute
            var result = await sender.Send(command, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning("Failed to delete message: {Error}", result.ErrorMessage);
                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "✓ Message {MessageId} deleted successfully by user {UserId}",
                messageId, userGuid);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    messageId,
                    deletedAt = result.DeletedAt
                },
                message = "Message deleted successfully"
            });
        })
        .RequireAuthorization()
        .WithName("DeleteMessage")
        .WithTags("Chats - Messages")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .WithSummary("Delete a message")
        .WithDescription(@"
            Delete a message (soft delete).

            Features:
            - Only sender can delete their own messages
            - Soft delete (IsDeleted flag, data preserved)
            - Updates conversation preview if this was the last message
            - Broadcasts delete notification to all users in conversation

            Permissions:
            - User can delete their own messages only
            - Admin delete can be added later with different permission

            Real-time:
            - All users in conversation receive delete notification via SignalR
            - Method: MessageDeleted(messageId, deletedBy, deletedAt)
            - Client should hide/remove the message from UI
        ");
    }
}
