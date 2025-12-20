namespace Chats.API.ChatHandler.DeleteMessage;

/// <summary>
/// Command để xóa tin nhắn (soft delete)
/// </summary>
public record DeleteMessageCommand(
    Guid MessageId,
    Guid UserId
) : ICommand<DeleteMessageResult>;

/// <summary>
/// Result sau khi delete message
/// </summary>
public record DeleteMessageResult
{
    public bool Success { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Handler để delete tin nhắn (soft delete)
/// Flow: Validate ownership → Soft delete in DB → Broadcast via SignalR
/// </summary>
internal class DeleteMessageHandler(
    IDbConnectionFactory dbFactory,
    IHubContext<ChatHub, IChatsClient> hubContext,
    ILogger<DeleteMessageHandler> logger)
    : ICommandHandler<DeleteMessageCommand, DeleteMessageResult>
{
    public async Task<DeleteMessageResult> Handle(
        DeleteMessageCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Deleting message {MessageId} by user {UserId}",
                request.MessageId, request.UserId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // GET MESSAGE AND VALIDATE OWNERSHIP
            // ================================================================
            var message = await connection.QueryFirstOrDefaultAsync<MessageInfo>(@"
                SELECT
                    m.Id,
                    m.ConversationId,
                    m.SenderId,
                    m.IsDeleted,
                    m.CreatedAt
                FROM messages m
                WHERE m.Id = @MessageId
                LIMIT 1",
                new { request.MessageId });

            if (message == null)
            {
                logger.LogWarning("Message {MessageId} not found", request.MessageId);
                return new DeleteMessageResult
                {
                    Success = false,
                    ErrorMessage = "Message not found"
                };
            }

            if (message.IsDeleted)
            {
                logger.LogInformation("Message {MessageId} already deleted", request.MessageId);
                return new DeleteMessageResult
                {
                    Success = true,
                    DeletedAt = DateTime.UtcNow
                };
            }

            // Check ownership - user can only delete their own messages
            // (Admin delete can be added later with different permission check)
            if (message.SenderId != request.UserId)
            {
                logger.LogWarning(
                    "User {UserId} attempted to delete message {MessageId} sent by {SenderId}",
                    request.UserId, request.MessageId, message.SenderId);

                return new DeleteMessageResult
                {
                    Success = false,
                    ErrorMessage = "You can only delete your own messages"
                };
            }

            // ================================================================
            // SOFT DELETE MESSAGE
            // ================================================================
            var now = DateTime.UtcNow;

            var deleteSql = @"
                UPDATE messages
                SET IsDeleted = 1,
                    DeletedAt = @DeletedAt,
                    DeletedBy = @DeletedBy,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @MessageId";

            await connection.ExecuteAsync(deleteSql, new
            {
                request.MessageId,
                DeletedAt = now,
                DeletedBy = request.UserId,
                UpdatedAt = now
            });

            logger.LogInformation(
                "✓ Message {MessageId} soft deleted successfully",
                request.MessageId);

            // ================================================================
            // UPDATE CONVERSATION PREVIEW IF THIS IS THE LAST MESSAGE
            // ================================================================
            await UpdateConversationPreviewIfNeededAsync(
                connection,
                message.ConversationId,
                request.MessageId);

            // ================================================================
            // BROADCAST VIA SIGNALR
            // ================================================================
            try
            {
                await hubContext.Clients
                    .Group(message.ConversationId.ToString())
                    .MessageDeleted(request.MessageId, request.UserId, now);

                logger.LogInformation(
                    "✓ Delete notification broadcasted for message {MessageId}",
                    request.MessageId);
            }
            catch (Exception signalREx)
            {
                logger.LogError(signalREx,
                    "Failed to broadcast delete notification for message {MessageId}",
                    request.MessageId);
            }

            return new DeleteMessageResult
            {
                Success = true,
                DeletedAt = now
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting message {MessageId}", request.MessageId);

            return new DeleteMessageResult
            {
                Success = false,
                ErrorMessage = $"Failed to delete message: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Update conversation preview if the deleted message was the last message
    /// Gets the new last message or clears preview
    /// </summary>
    private async Task UpdateConversationPreviewIfNeededAsync(
        IDbConnection connection,
        Guid conversationId,
        Guid deletedMessageId)
    {
        // Get the current last message ID
        var currentLastMessageId = await connection.ExecuteScalarAsync<Guid?>(@"
            SELECT
                (SELECT Id FROM messages
                 WHERE ConversationId = @ConversationId AND IsDeleted = 0
                 ORDER BY CreatedAt DESC
                 LIMIT 1)",
            new { conversationId });

        // If the deleted message was the last one, we need to update preview
        // Check by getting the most recent non-deleted message
        var newLastMessage = await connection.QueryFirstOrDefaultAsync<LastMessageInfo>(@"
            SELECT
                m.Id,
                m.Content,
                m.SenderId,
                m.SenderName,
                m.CreatedAt as LastMessageAt
            FROM messages m
            WHERE m.ConversationId = @ConversationId
              AND m.IsDeleted = 0
            ORDER BY m.CreatedAt DESC
            LIMIT 1",
            new { conversationId });

        if (newLastMessage != null)
        {
            // Update with new last message
            var preview = newLastMessage.Content?.Length > 150
                ? newLastMessage.Content.Substring(0, 147) + "..."
                : newLastMessage.Content;

            var updateSql = @"
                UPDATE conversations
                SET LastMessageAt = @LastMessageAt,
                    LastMessagePreview = @Preview,
                    LastMessageSenderId = @SenderId,
                    LastMessageSenderName = @SenderName,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @ConversationId";

            await connection.ExecuteAsync(updateSql, new
            {
                conversationId,
                newLastMessage.LastMessageAt,
                Preview = preview,
                newLastMessage.SenderId,
                newLastMessage.SenderName,
                UpdatedAt = DateTime.UtcNow
            });

            logger.LogDebug(
                "Updated conversation preview with new last message {MessageId}",
                newLastMessage.Id);
        }
        else
        {
            // No messages left, clear preview
            var clearSql = @"
                UPDATE conversations
                SET LastMessageAt = NULL,
                    LastMessagePreview = NULL,
                    LastMessageSenderId = NULL,
                    LastMessageSenderName = NULL,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @ConversationId";

            await connection.ExecuteAsync(clearSql, new
            {
                conversationId,
                UpdatedAt = DateTime.UtcNow
            });

            logger.LogDebug(
                "Cleared conversation preview (no messages left) for conversation {ConversationId}",
                conversationId);
        }
    }
}

/// <summary>
/// DTO để lấy message info
/// </summary>
internal record MessageInfo
{
    public Guid Id { get; init; }
    public Guid ConversationId { get; init; }
    public Guid SenderId { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// DTO cho last message info
/// </summary>
internal record LastMessageInfo
{
    public Guid Id { get; init; }
    public string? Content { get; init; }
    public Guid SenderId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public DateTime LastMessageAt { get; init; }
}
