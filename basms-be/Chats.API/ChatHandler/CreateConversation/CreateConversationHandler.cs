using Dapper;

namespace Chats.API.ChatHandler.CreateConversation;

/// <summary>
/// Command để tạo conversation mới
/// </summary>
public record CreateConversationCommand(
    string ConversationType,
    List<Guid> ParticipantIds,
    List<ParticipantInfo>? Participants = null,
    string? ConversationName = null,
    Guid? ShiftId = null,
    Guid? IncidentId = null,
    Guid? TeamId = null,
    Guid? ContractId = null
) : ICommand<CreateConversationResult>;

/// <summary>
/// Participant info from frontend
/// </summary>
public record ParticipantInfo
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? UserAvatarUrl { get; init; }
    public string? UserRole { get; init; }
}

/// <summary>
/// Result sau khi tạo conversation
/// </summary>
public record CreateConversationResult
{
    public bool Success { get; init; }
    public Guid? ConversationId { get; init; }
    public ConversationDto? Conversation { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsExisting { get; init; } // True nếu conversation đã tồn tại
}

/// <summary>
/// Handler để tạo conversation mới
/// Flow:
/// 1. Validate input
/// 2. For DIRECT conversations: Check if already exists between same participants
/// 3. Create conversation
/// 4. Add participants
/// 5. Return conversation DTO
/// </summary>
internal class CreateConversationHandler(
    IDbConnectionFactory dbFactory,
    ILogger<CreateConversationHandler> logger)
    : ICommandHandler<CreateConversationCommand, CreateConversationResult>
{
    public async Task<CreateConversationResult> Handle(
        CreateConversationCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ================================================================
            // VALIDATION
            // ================================================================
            if (request.ParticipantIds == null || request.ParticipantIds.Count == 0)
            {
                return new CreateConversationResult
                {
                    Success = false,
                    ErrorMessage = "At least one participant is required"
                };
            }

            if (string.IsNullOrWhiteSpace(request.ConversationType))
            {
                return new CreateConversationResult
                {
                    Success = false,
                    ErrorMessage = "ConversationType is required"
                };
            }

            // DIRECT conversation must have exactly 2 participants
            if (request.ConversationType.ToUpper() == "DIRECT" && request.ParticipantIds.Count != 2)
            {
                return new CreateConversationResult
                {
                    Success = false,
                    ErrorMessage = "DIRECT conversation must have exactly 2 participants"
                };
            }

            logger.LogInformation(
                "Creating conversation: Type={ConversationType}, Participants={ParticipantCount}",
                request.ConversationType, request.ParticipantIds.Count);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // CHECK IF DIRECT CONVERSATION ALREADY EXISTS
            // ================================================================
            if (request.ConversationType.ToUpper() == "DIRECT")
            {
                var existingConversationId = await CheckExistingDirectConversationAsync(
                    connection,
                    request.ParticipantIds);

                if (existingConversationId.HasValue)
                {
                    logger.LogInformation(
                        "Found existing DIRECT conversation {ConversationId} between participants",
                        existingConversationId.Value);

                    var existingConversation = await GetConversationDtoAsync(connection, existingConversationId.Value);

                    return new CreateConversationResult
                    {
                        Success = true,
                        ConversationId = existingConversationId.Value,
                        Conversation = existingConversation,
                        IsExisting = true
                    };
                }
            }

            // ================================================================
            // CREATE NEW CONVERSATION
            // ================================================================
            var conversationId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var insertConversationSql = @"
                INSERT INTO conversations (
                    Id, ConversationType, ConversationName,
                    ShiftId, IncidentId, TeamId, ContractId,
                    IsActive, IsDeleted, CreatedAt, UpdatedAt
                ) VALUES (
                    @Id, @ConversationType, @ConversationName,
                    @ShiftId, @IncidentId, @TeamId, @ContractId,
                    1, 0, @CreatedAt, @UpdatedAt
                )";

            await connection.ExecuteAsync(insertConversationSql, new
            {
                Id = conversationId,
                request.ConversationType,
                request.ConversationName,
                request.ShiftId,
                request.IncidentId,
                request.TeamId,
                request.ContractId,
                CreatedAt = now,
                UpdatedAt = now
            });

            logger.LogInformation(
                "✓ Conversation {ConversationId} created successfully",
                conversationId);

            // ================================================================
            // ADD PARTICIPANTS
            // ================================================================
            await AddParticipantsAsync(connection, conversationId, request.ParticipantIds, request.Participants, now);

            // ================================================================
            // GET FULL CONVERSATION DTO
            // ================================================================
            var conversationDto = await GetConversationDtoAsync(connection, conversationId);

            return new CreateConversationResult
            {
                Success = true,
                ConversationId = conversationId,
                Conversation = conversationDto,
                IsExisting = false
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating conversation");

            return new CreateConversationResult
            {
                Success = false,
                ErrorMessage = $"Failed to create conversation: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Check if DIRECT conversation already exists between same participants
    /// </summary>
    private async Task<Guid?> CheckExistingDirectConversationAsync(
        IDbConnection connection,
        List<Guid> participantIds)
    {
        var sql = @"
            SELECT c.Id
            FROM conversations c
            WHERE c.ConversationType = 'DIRECT'
              AND c.IsDeleted = 0
              AND c.Id IN (
                  SELECT ConversationId
                  FROM conversation_participants
                  WHERE UserId = @UserId1
                    AND IsActive = 1
              )
              AND c.Id IN (
                  SELECT ConversationId
                  FROM conversation_participants
                  WHERE UserId = @UserId2
                    AND IsActive = 1
              )
            LIMIT 1";

        return await connection.QueryFirstOrDefaultAsync<Guid?>(sql, new
        {
            UserId1 = participantIds[0],
            UserId2 = participantIds[1]
        });
    }

    /// <summary>
    /// Add participants to conversation
    /// Uses participant info from frontend if available, otherwise uses default values
    /// </summary>
    private async Task AddParticipantsAsync(
        IDbConnection connection,
        Guid conversationId,
        List<Guid> participantIds,
        List<ParticipantInfo>? participantsInfo,
        DateTime now)
    {
        var insertParticipantSql = @"
            INSERT INTO conversation_participants (
                Id, ConversationId, UserId,
                UserName, UserAvatarUrl, UserRole, Role,
                JoinedAt, IsActive, CreatedAt, UpdatedAt
            ) VALUES (
                @Id, @ConversationId, @UserId,
                @UserName, @UserAvatarUrl, @UserRole, @Role,
                @JoinedAt, 1, @CreatedAt, @UpdatedAt
            )";

        foreach (var participantId in participantIds)
        {
            var participantDbId = Guid.NewGuid();

            // Get participant info from frontend if available
            var participantInfo = participantsInfo?.FirstOrDefault(p => p.UserId == participantId);

            var userName = participantInfo?.UserName ?? $"User {participantId.ToString().Substring(0, 8)}";
            var userAvatarUrl = participantInfo?.UserAvatarUrl;
            var userRole = participantInfo?.UserRole ?? "MEMBER";

            await connection.ExecuteAsync(insertParticipantSql, new
            {
                Id = participantDbId,
                ConversationId = conversationId,
                UserId = participantId,
                UserName = userName,
                UserAvatarUrl = userAvatarUrl,
                UserRole = userRole,
                Role = "MEMBER",
                JoinedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });

            logger.LogDebug(
                "Added participant {UserId} ({UserName}) to conversation {ConversationId}",
                participantId, userName, conversationId);
        }

        logger.LogInformation(
            "✓ Added {Count} participants to conversation {ConversationId}",
            participantIds.Count, conversationId);
    }

    /// <summary>
    /// Get full conversation DTO
    /// </summary>
    private async Task<ConversationDto?> GetConversationDtoAsync(
        IDbConnection connection,
        Guid conversationId)
    {
        var sql = @"
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
            WHERE c.Id = @ConversationId
              AND c.IsDeleted = 0
            LIMIT 1";

        return await connection.QueryFirstOrDefaultAsync<ConversationDto>(
            sql,
            new { ConversationId = conversationId });
    }
}

/// <summary>
/// DTO cho conversation (shared with GetAllConversations)
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
