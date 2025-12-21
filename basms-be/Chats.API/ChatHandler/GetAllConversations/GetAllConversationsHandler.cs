using Dapper;

namespace Chats.API.ChatHandler.GetAllConversations;

/// <summary>
/// Query để lấy danh sách conversations mà user hiện tại tham gia
/// Sắp xếp theo: LastMessageAt giảm dần (cuộc trò chuyện có tin nhắn mới nhất ở đầu)
/// </summary>
public record GetAllConversationsQuery(Guid UserId) : IQuery<GetAllConversationsResult>;

/// <summary>
/// Result chứa danh sách conversations
/// </summary>
public record GetAllConversationsResult
{
    public bool Success { get; init; }
    public List<ConversationDto> Conversations { get; init; } = new();
    public int TotalCount { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO cho conversation
/// </summary>
public record ConversationDto
{
    public Guid Id { get; init; }
    public string ConversationType { get; init; } = string.Empty;
    public string? ConversationName { get; init; }

    // Linked Entities
    public Guid? ShiftId { get; init; }
    public Guid? IncidentId { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? ContractId { get; init; }

    // Status
    public bool IsActive { get; init; }

    // Last Message Info
    public DateTime? LastMessageAt { get; init; }
    public string? LastMessagePreview { get; init; }
    public Guid? LastMessageSenderId { get; init; }
    public string? LastMessageSenderName { get; init; }

    // Audit
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
}

/// <summary>
/// Handler để lấy danh sách tất cả conversations
/// </summary>
internal class GetAllConversationsHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetAllConversationsHandler> logger)
    : IQueryHandler<GetAllConversationsQuery, GetAllConversationsResult>
{
    public async Task<GetAllConversationsResult> Handle(
        GetAllConversationsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting conversations for UserId: {UserId}", request.UserId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // COUNT TOTAL RECORDS WHERE USER IS PARTICIPANT
            // ================================================================
            var countSql = @"
                SELECT COUNT(DISTINCT c.Id)
                FROM conversations c
                INNER JOIN conversation_participants cp ON c.Id = cp.ConversationId
                WHERE c.IsDeleted = 0
                  AND cp.UserId = @UserId
                  AND cp.IsActive = 1";

            var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { request.UserId });

            logger.LogInformation(
                "Total conversations found for user: {TotalCount}",
                totalCount);

            // ================================================================
            // GET CONVERSATIONS WHERE USER IS PARTICIPANT
            // SORTED BY LAST MESSAGE TIME DESCENDING
            // ================================================================

            var sql = @"
                SELECT DISTINCT
                    c.Id,
                    c.ConversationType,
                    c.ConversationName,
                    c.ShiftId,
                    c.IncidentId,
                    c.TeamId,
                    c.ContractId,
                    c.IsActive,
                    c.LastMessageAt,
                    c.LastMessagePreview,
                    c.LastMessageSenderId,
                    c.LastMessageSenderName,
                    c.IsDeleted,
                    c.CreatedAt,
                    c.UpdatedAt,
                    c.CreatedBy
                FROM conversations c
                INNER JOIN conversation_participants cp ON c.Id = cp.ConversationId
                WHERE c.IsDeleted = 0
                  AND cp.UserId = @UserId
                  AND cp.IsActive = 1
                ORDER BY
                    CASE WHEN c.LastMessageAt IS NULL THEN 1 ELSE 0 END,
                    c.LastMessageAt DESC,
                    c.CreatedAt DESC";

            var conversations = await connection.QueryAsync<ConversationDto>(sql, new { request.UserId });
            var conversationsList = conversations.ToList();

            logger.LogInformation(
                "Retrieved {Count} conversations for user {UserId} sorted by last message time",
                conversationsList.Count,
                request.UserId);

            return new GetAllConversationsResult
            {
                Success = true,
                Conversations = conversationsList,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all conversations");

            return new GetAllConversationsResult
            {
                Success = false,
                Conversations = new List<ConversationDto>(),
                TotalCount = 0,
                ErrorMessage = $"Failed to get conversations: {ex.Message}"
            };
        }
    }
}
