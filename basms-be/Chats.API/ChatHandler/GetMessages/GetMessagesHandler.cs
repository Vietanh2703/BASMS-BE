namespace Chats.API.ChatHandler.GetMessages;

/// <summary>
/// Query để lấy danh sách messages của conversation
/// Support pagination với cursor-based (load older messages)
/// </summary>
public record GetMessagesQuery(
    Guid ConversationId,
    Guid UserId,
    int Limit = 50,
    Guid? BeforeMessageId = null // Cursor for pagination (load older messages)
) : IQuery<GetMessagesResult>;

/// <summary>
/// Result chứa danh sách messages
/// </summary>
public record GetMessagesResult
{
    public bool Success { get; init; }
    public List<MessageDto> Messages { get; init; } = new();
    public int TotalCount { get; init; }
    public bool HasMore { get; init; }
    public Guid? OldestMessageId { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Handler để lấy messages với pagination
/// Messages được sắp xếp theo thời gian giảm dần (newest first)
/// </summary>
internal class GetMessagesHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetMessagesHandler> logger)
    : IQueryHandler<GetMessagesQuery, GetMessagesResult>
{
    public async Task<GetMessagesResult> Handle(
        GetMessagesQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting messages for conversation {ConversationId}, limit={Limit}, beforeMessageId={BeforeMessageId}",
                request.ConversationId, request.Limit, request.BeforeMessageId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // CHECK USER IS PARTICIPANT
            // ================================================================
            var isParticipant = await connection.ExecuteScalarAsync<bool>(@"
                SELECT COUNT(*) > 0
                FROM conversation_participants
                WHERE ConversationId = @ConversationId
                  AND UserId = @UserId
                  AND IsActive = 1",
                new { request.ConversationId, request.UserId });

            if (!isParticipant)
            {
                logger.LogWarning(
                    "User {UserId} is not a participant of conversation {ConversationId}",
                    request.UserId, request.ConversationId);

                return new GetMessagesResult
                {
                    Success = false,
                    ErrorMessage = "User is not a participant of this conversation"
                };
            }

            // ================================================================
            // GET TOTAL COUNT
            // ================================================================
            var totalCount = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)
                FROM messages
                WHERE ConversationId = @ConversationId
                  AND IsDeleted = 0",
                new { request.ConversationId });

            // ================================================================
            // PREPARE PAGINATION QUERY
            // ================================================================
            string sql;
            object queryParams;

            if (request.BeforeMessageId.HasValue)
            {
                // Get messages older than the specified message (cursor-based pagination)
                // First, get the CreatedAt of the cursor message
                var cursorTime = await connection.ExecuteScalarAsync<DateTime?>(@"
                    SELECT CreatedAt
                    FROM messages
                    WHERE Id = @MessageId
                    LIMIT 1",
                    new { MessageId = request.BeforeMessageId.Value });

                if (!cursorTime.HasValue)
                {
                    logger.LogWarning("Cursor message {MessageId} not found", request.BeforeMessageId);
                    return new GetMessagesResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid cursor message"
                    };
                }

                sql = @"
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
                    WHERE m.ConversationId = @ConversationId
                      AND m.IsDeleted = 0
                      AND m.CreatedAt < @CursorTime
                    ORDER BY m.CreatedAt DESC
                    LIMIT @Limit";

                queryParams = new
                {
                    request.ConversationId,
                    CursorTime = cursorTime.Value,
                    Limit = request.Limit + 1 // +1 to check if there are more
                };
            }
            else
            {
                // Get latest messages (initial load)
                sql = @"
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
                    WHERE m.ConversationId = @ConversationId
                      AND m.IsDeleted = 0
                    ORDER BY m.CreatedAt DESC
                    LIMIT @Limit";

                queryParams = new
                {
                    request.ConversationId,
                    Limit = request.Limit + 1 // +1 to check if there are more
                };
            }

            // ================================================================
            // EXECUTE QUERY
            // ================================================================
            var messages = await connection.QueryAsync<MessageDto>(sql, queryParams);
            var messagesList = messages.ToList();

            // Check if there are more messages
            var hasMore = messagesList.Count > request.Limit;
            if (hasMore)
            {
                messagesList = messagesList.Take(request.Limit).ToList();
            }

            // Convert DateTime fields to Vietnam time
            messagesList = messagesList.Select(m => m with
            {
                CreatedAt = m.CreatedAt.ToVietnamTime(),
                EditedAt = m.EditedAt.ToVietnamTime()
            }).ToList();

            // Get oldest message ID for next cursor
            var oldestMessageId = messagesList.LastOrDefault()?.Id;

            logger.LogInformation(
                "✓ Retrieved {Count} messages for conversation {ConversationId}. HasMore: {HasMore}",
                messagesList.Count, request.ConversationId, hasMore);

            return new GetMessagesResult
            {
                Success = true,
                Messages = messagesList,
                TotalCount = totalCount,
                HasMore = hasMore,
                OldestMessageId = oldestMessageId
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting messages for conversation {ConversationId}",
                request.ConversationId);

            return new GetMessagesResult
            {
                Success = false,
                ErrorMessage = $"Failed to get messages: {ex.Message}"
            };
        }
    }
}
