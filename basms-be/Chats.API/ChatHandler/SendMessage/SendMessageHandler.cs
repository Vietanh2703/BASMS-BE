namespace Chats.API.ChatHandler.SendMessage;

/// <summary>
/// Command để gửi tin nhắn text
/// </summary>
public record SendMessageCommand(
    Guid ConversationId,
    Guid SenderId,
    string Content,
    Guid? ReplyToMessageId = null
) : ICommand<SendMessageResult>;

/// <summary>
/// Result sau khi gửi tin nhắn
/// </summary>
public record SendMessageResult
{
    public bool Success { get; init; }
    public Guid? MessageId { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public MessageDto? Message { get; init; }
}

/// <summary>
/// Handler để gửi tin nhắn text
/// Flow: Save to DB → Update conversation → Broadcast via SignalR
/// </summary>
internal class SendMessageHandler(
    IDbConnectionFactory dbFactory,
    IHubContext<ChatHub, IChatsClient> hubContext,
    ILogger<SendMessageHandler> logger)
    : ICommandHandler<SendMessageCommand, SendMessageResult>
{
    public async Task<SendMessageResult> Handle(
        SendMessageCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ================================================================
            // VALIDATION
            // ================================================================
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return new SendMessageResult
                {
                    Success = false,
                    ErrorMessage = "Message content cannot be empty"
                };
            }

            if (request.Content.Length > 10000)
            {
                return new SendMessageResult
                {
                    Success = false,
                    ErrorMessage = "Message content exceeds maximum length (10000 characters)"
                };
            }

            logger.LogInformation(
                "Sending message to conversation {ConversationId} from user {SenderId}",
                request.ConversationId, request.SenderId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // CHECK CONVERSATION EXISTS
            // ================================================================
            var conversationExists = await connection.ExecuteScalarAsync<bool>(@"
                SELECT COUNT(*) > 0
                FROM conversations
                WHERE Id = @ConversationId AND IsDeleted = 0",
                new { request.ConversationId });

            if (!conversationExists)
            {
                logger.LogWarning(
                    "Conversation {ConversationId} not found",
                    request.ConversationId);

                return new SendMessageResult
                {
                    Success = false,
                    ErrorMessage = "Conversation not found"
                };
            }

            // ================================================================
            // CHECK USER IS PARTICIPANT
            // ================================================================
            var isParticipant = await connection.ExecuteScalarAsync<bool>(@"
                SELECT COUNT(*) > 0
                FROM conversation_participants
                WHERE ConversationId = @ConversationId
                  AND UserId = @UserId
                  AND IsActive = 1",
                new { request.ConversationId, UserId = request.SenderId });

            if (!isParticipant)
            {
                logger.LogWarning(
                    "User {UserId} is not a participant of conversation {ConversationId}",
                    request.SenderId, request.ConversationId);

                return new SendMessageResult
                {
                    Success = false,
                    ErrorMessage = "User is not a participant of this conversation"
                };
            }

            // ================================================================
            // GET SENDER INFO (for caching in message)
            // ================================================================
            var senderInfo = await connection.QueryFirstOrDefaultAsync<SenderInfo>(@"
                SELECT
                    cp.UserName as SenderName,
                    cp.UserAvatarUrl as SenderAvatarUrl
                FROM conversation_participants cp
                WHERE cp.ConversationId = @ConversationId
                  AND cp.UserId = @UserId
                LIMIT 1",
                new { request.ConversationId, UserId = request.SenderId });

            var senderName = senderInfo?.SenderName ?? "Unknown User";
            var senderAvatarUrl = senderInfo?.SenderAvatarUrl;

            // ================================================================
            // CREATE MESSAGE
            // ================================================================
            var messageId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var insertSql = @"
                INSERT INTO messages (
                    Id, ConversationId, SenderId, SenderName, SenderAvatarUrl,
                    MessageType, Content, ReplyToMessageId,
                    IsEdited, IsDeleted, CreatedAt, UpdatedAt
                ) VALUES (
                    @Id, @ConversationId, @SenderId, @SenderName, @SenderAvatarUrl,
                    @MessageType, @Content, @ReplyToMessageId,
                    0, 0, @CreatedAt, @UpdatedAt
                )";

            await connection.ExecuteAsync(insertSql, new
            {
                Id = messageId,
                request.ConversationId,
                request.SenderId,
                SenderName = senderName,
                SenderAvatarUrl = senderAvatarUrl,
                MessageType = "TEXT",
                request.Content,
                request.ReplyToMessageId,
                CreatedAt = now,
                UpdatedAt = now
            });

            logger.LogInformation(
                "✓ Message {MessageId} created in conversation {ConversationId}",
                messageId, request.ConversationId);

            // ================================================================
            // UPDATE CONVERSATION LAST MESSAGE
            // ================================================================
            await UpdateConversationLastMessageAsync(
                connection,
                request.ConversationId,
                messageId,
                request.Content,
                request.SenderId,
                senderName,
                now);

            // ================================================================
            // GET FULL MESSAGE DTO FOR BROADCAST
            // ================================================================
            var messageDto = await GetMessageDtoAsync(connection, messageId);

            if (messageDto == null)
            {
                logger.LogWarning("Failed to retrieve message {MessageId} after creation", messageId);
                return new SendMessageResult
                {
                    Success = false,
                    ErrorMessage = "Failed to retrieve created message"
                };
            }

            // ================================================================
            // BROADCAST VIA SIGNALR
            // ================================================================
            try
            {
                await hubContext.Clients
                    .Group(request.ConversationId.ToString())
                    .ReceiveMessage(messageDto);

                logger.LogInformation(
                    "✓ Message {MessageId} broadcasted to conversation group {ConversationId}",
                    messageId, request.ConversationId);
            }
            catch (Exception signalREx)
            {
                // Don't fail the entire operation if SignalR broadcast fails
                logger.LogError(signalREx,
                    "Failed to broadcast message {MessageId} via SignalR",
                    messageId);
            }

            return new SendMessageResult
            {
                Success = true,
                MessageId = messageId,
                CreatedAt = now.ToVietnamTime(),
                Message = messageDto
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error sending message to conversation {ConversationId}",
                request.ConversationId);

            return new SendMessageResult
            {
                Success = false,
                ErrorMessage = $"Failed to send message: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Update conversation's last message info for preview
    /// </summary>
    private async Task UpdateConversationLastMessageAsync(
        IDbConnection connection,
        Guid conversationId,
        Guid messageId,
        string content,
        Guid senderId,
        string senderName,
        DateTime timestamp)
    {
        // Truncate content to 150 characters for preview
        var preview = content.Length > 150
            ? content.Substring(0, 147) + "..."
            : content;

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
            ConversationId = conversationId,
            LastMessageAt = timestamp,
            Preview = preview,
            SenderId = senderId,
            SenderName = senderName,
            UpdatedAt = timestamp
        });

        logger.LogDebug(
            "Updated last message preview for conversation {ConversationId}",
            conversationId);
    }

    /// <summary>
    /// Get full message DTO from database
    /// </summary>
    private async Task<MessageDto?> GetMessageDtoAsync(IDbConnection connection, Guid messageId)
    {
        var sql = @"
            SELECT
                m.Id,
                m.ConversationId,
                m.SenderId,
                m.SenderName,
                m.SenderAvatarUrl,
                m.MessageType,
                m.Content,
                m.FileUrl,
                m.FileName,
                m.FileSize,
                m.FileType,
                m.ThumbnailUrl,
                m.Latitude,
                m.Longitude,
                m.LocationAddress,
                m.LocationMapUrl,
                m.ReplyToMessageId,
                m.IsEdited,
                m.EditedAt,
                m.CreatedAt
            FROM messages m
            WHERE m.Id = @MessageId
              AND m.IsDeleted = 0
            LIMIT 1";

        var message = await connection.QueryFirstOrDefaultAsync<MessageDto>(sql, new { MessageId = messageId });

        // Convert DateTime fields to Vietnam time
        if (message != null)
        {
            return message with
            {
                CreatedAt = message.CreatedAt.ToVietnamTime(),
                EditedAt = message.EditedAt.ToVietnamTime()
            };
        }

        return message;
    }
}

/// <summary>
/// DTO để lấy sender info
/// </summary>
internal record SenderInfo
{
    public string SenderName { get; init; } = string.Empty;
    public string? SenderAvatarUrl { get; init; }
}
