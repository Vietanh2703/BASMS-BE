namespace Chats.API.ChatHandler.EditMessage;

/// <summary>
/// Command để chỉnh sửa tin nhắn
/// </summary>
public record EditMessageCommand(
    Guid MessageId,
    Guid UserId,
    string NewContent
) : ICommand<EditMessageResult>;

/// <summary>
/// Result sau khi edit message
/// </summary>
public record EditMessageResult
{
    public bool Success { get; init; }
    public DateTime? EditedAt { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Handler để edit tin nhắn
/// Flow: Validate ownership → Update DB → Broadcast via SignalR
/// </summary>
internal class EditMessageHandler(
    IDbConnectionFactory dbFactory,
    IHubContext<ChatHub, IChatsClient> hubContext,
    ILogger<EditMessageHandler> logger)
    : ICommandHandler<EditMessageCommand, EditMessageResult>
{
    public async Task<EditMessageResult> Handle(
        EditMessageCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ================================================================
            // VALIDATION
            // ================================================================
            if (string.IsNullOrWhiteSpace(request.NewContent))
            {
                return new EditMessageResult
                {
                    Success = false,
                    ErrorMessage = "Message content cannot be empty"
                };
            }

            if (request.NewContent.Length > 10000)
            {
                return new EditMessageResult
                {
                    Success = false,
                    ErrorMessage = "Message content exceeds maximum length (10000 characters)"
                };
            }

            logger.LogInformation(
                "Editing message {MessageId} by user {UserId}",
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
                    m.Content,
                    m.MessageType,
                    m.IsDeleted
                FROM messages m
                WHERE m.Id = @MessageId
                LIMIT 1",
                new { request.MessageId });

            if (message == null)
            {
                logger.LogWarning("Message {MessageId} not found", request.MessageId);
                return new EditMessageResult
                {
                    Success = false,
                    ErrorMessage = "Message not found"
                };
            }

            if (message.IsDeleted)
            {
                logger.LogWarning("Cannot edit deleted message {MessageId}", request.MessageId);
                return new EditMessageResult
                {
                    Success = false,
                    ErrorMessage = "Cannot edit deleted message"
                };
            }

            if (message.SenderId != request.UserId)
            {
                logger.LogWarning(
                    "User {UserId} attempted to edit message {MessageId} sent by {SenderId}",
                    request.UserId, request.MessageId, message.SenderId);

                return new EditMessageResult
                {
                    Success = false,
                    ErrorMessage = "You can only edit your own messages"
                };
            }

            // Only allow editing TEXT messages
            if (message.MessageType != "TEXT")
            {
                logger.LogWarning(
                    "Cannot edit non-text message {MessageId} of type {MessageType}",
                    request.MessageId, message.MessageType);

                return new EditMessageResult
                {
                    Success = false,
                    ErrorMessage = "Only text messages can be edited"
                };
            }

            // Check if content is actually different
            if (message.Content == request.NewContent)
            {
                logger.LogInformation("Content unchanged for message {MessageId}", request.MessageId);
                return new EditMessageResult
                {
                    Success = true,
                    EditedAt = DateTime.UtcNow.ToVietnamTime()
                };
            }

            // ================================================================
            // UPDATE MESSAGE
            // ================================================================
            var now = DateTime.UtcNow;

            var updateSql = @"
                UPDATE messages
                SET Content = @NewContent,
                    IsEdited = 1,
                    EditedAt = @EditedAt,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @MessageId";

            await connection.ExecuteAsync(updateSql, new
            {
                request.MessageId,
                request.NewContent,
                EditedAt = now,
                UpdatedAt = now
            });

            logger.LogInformation(
                "✓ Message {MessageId} edited successfully",
                request.MessageId);

            // ================================================================
            // UPDATE CONVERSATION PREVIEW IF THIS IS THE LAST MESSAGE
            // ================================================================
            await UpdateConversationPreviewIfNeededAsync(
                connection,
                message.ConversationId,
                request.MessageId,
                request.NewContent);

            // ================================================================
            // BROADCAST VIA SIGNALR
            // ================================================================
            try
            {
                await hubContext.Clients
                    .Group(message.ConversationId.ToString())
                    .MessageEdited(request.MessageId, request.NewContent, now.ToVietnamTime());

                logger.LogInformation(
                    "✓ Edit notification broadcasted for message {MessageId}",
                    request.MessageId);
            }
            catch (Exception signalREx)
            {
                logger.LogError(signalREx,
                    "Failed to broadcast edit notification for message {MessageId}",
                    request.MessageId);
            }

            return new EditMessageResult
            {
                Success = true,
                EditedAt = now.ToVietnamTime()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error editing message {MessageId}", request.MessageId);

            return new EditMessageResult
            {
                Success = false,
                ErrorMessage = $"Failed to edit message: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Update conversation preview if the edited message is the last message
    /// </summary>
    private async Task UpdateConversationPreviewIfNeededAsync(
        IDbConnection connection,
        Guid conversationId,
        Guid messageId,
        string newContent)
    {
        // Check if this is the last message
        var isLastMessage = await connection.ExecuteScalarAsync<bool>(@"
            SELECT
                (SELECT Id FROM messages
                 WHERE ConversationId = @ConversationId AND IsDeleted = 0
                 ORDER BY CreatedAt DESC
                 LIMIT 1) = @MessageId",
            new { conversationId, messageId });

        if (!isLastMessage)
        {
            return;
        }

        // Update preview
        var preview = newContent.Length > 150
            ? newContent.Substring(0, 147) + "..."
            : newContent;

        var updateSql = @"
            UPDATE conversations
            SET LastMessagePreview = @Preview,
                UpdatedAt = @UpdatedAt
            WHERE Id = @ConversationId";

        await connection.ExecuteAsync(updateSql, new
        {
            conversationId,
            Preview = preview,
            UpdatedAt = DateTime.UtcNow
        });

        logger.LogDebug(
            "Updated conversation preview after editing last message {MessageId}",
            messageId);
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
    public string Content { get; init; } = string.Empty;
    public string MessageType { get; init; } = string.Empty;
    public bool IsDeleted { get; init; }
}
