using Dapper;

namespace Chats.API.ChatHandler.GetAllConversations;

/// <summary>
/// Query để lấy danh sách tất cả conversations
/// Sắp xếp theo: LastMessageAt giảm dần (cuộc trò chuyện có tin nhắn mới nhất ở đầu)
/// </summary>
public record GetAllConversationsQuery(
    Guid? UserId = null,
    string? ConversationType = null,
    Guid? ShiftId = null,
    Guid? IncidentId = null,
    Guid? TeamId = null,
    Guid? ContractId = null,
    bool? IsActive = null
) : IQuery<GetAllConversationsResult>;

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
/// Handler để lấy danh sách tất cả conversations với filtering
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
            logger.LogInformation(
                "Getting all conversations: UserId={UserId}, Type={Type}, ShiftId={ShiftId}, IncidentId={IncidentId}",
                request.UserId?.ToString() ?? "ALL",
                request.ConversationType ?? "ALL",
                request.ShiftId?.ToString() ?? "ALL",
                request.IncidentId?.ToString() ?? "ALL");

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BUILD DYNAMIC SQL QUERY
            // ================================================================
            var whereClauses = new List<string> { "c.IsDeleted = 0" };
            var parameters = new DynamicParameters();

            // Filter by user participation (if UserId provided)
            if (request.UserId.HasValue)
            {
                whereClauses.Add(@"EXISTS (
                    SELECT 1 FROM conversation_participants cp
                    WHERE cp.ConversationId = c.Id
                    AND cp.UserId = @UserId
                    AND cp.IsActive = 1
                )");
                parameters.Add("UserId", request.UserId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.ConversationType))
            {
                whereClauses.Add("c.ConversationType = @ConversationType");
                parameters.Add("ConversationType", request.ConversationType);
            }

            if (request.ShiftId.HasValue)
            {
                whereClauses.Add("c.ShiftId = @ShiftId");
                parameters.Add("ShiftId", request.ShiftId.Value);
            }

            if (request.IncidentId.HasValue)
            {
                whereClauses.Add("c.IncidentId = @IncidentId");
                parameters.Add("IncidentId", request.IncidentId.Value);
            }

            if (request.TeamId.HasValue)
            {
                whereClauses.Add("c.TeamId = @TeamId");
                parameters.Add("TeamId", request.TeamId.Value);
            }

            if (request.ContractId.HasValue)
            {
                whereClauses.Add("c.ContractId = @ContractId");
                parameters.Add("ContractId", request.ContractId.Value);
            }

            if (request.IsActive.HasValue)
            {
                whereClauses.Add("c.IsActive = @IsActive");
                parameters.Add("IsActive", request.IsActive.Value);
            }

            var whereClause = string.Join(" AND ", whereClauses);

            // ================================================================
            // COUNT TOTAL RECORDS
            // ================================================================
            var countSql = $@"
                SELECT COUNT(*)
                FROM conversations c
                WHERE {whereClause}";

            var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

            logger.LogInformation(
                "Total conversations found: {TotalCount}",
                totalCount);

            // ================================================================
            // GET ALL DATA - SORTED BY LAST MESSAGE TIME DESCENDING
            // ================================================================

            var sql = $@"
                SELECT
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
                WHERE {whereClause}
                ORDER BY
                    c.LastMessageAt DESC NULLS LAST,
                    c.CreatedAt DESC";

            var conversations = await connection.QueryAsync<ConversationDto>(sql, parameters);
            var conversationsList = conversations.ToList();

            logger.LogInformation(
                "Retrieved {Count} conversations sorted by last message time (newest first)",
                conversationsList.Count);

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
