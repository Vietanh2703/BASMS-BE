namespace Chats.API.ChatHandler.CreateConversation;

/// <summary>
/// Endpoint để tạo conversation mới
/// </summary>
public class CreateConversationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chats/conversations", async (
            CreateConversationRequest request,
            ISender sender,
            ILogger<CreateConversationEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "POST /api/chats/conversations - Creating conversation: Type={ConversationType}, Participants={ParticipantCount}",
                request.ConversationType, request.ParticipantIds?.Count ?? 0);

            var command = new CreateConversationCommand(
                request.ConversationType,
                request.ParticipantIds ?? new List<Guid>(),
                request.Participants,
                request.ConversationName,
                request.ShiftId,
                request.IncidentId,
                request.TeamId,
                request.ContractId
            );

            var result = await sender.Send(command, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to create conversation: {Error}",
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "✓ Conversation {ConversationId} {Status}",
                result.ConversationId,
                result.IsExisting ? "already exists (returned existing)" : "created successfully");

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    conversation = result.Conversation,
                    conversationId = result.ConversationId,
                    isExisting = result.IsExisting
                },
                message = result.IsExisting
                    ? "Conversation already exists"
                    : "Conversation created successfully"
            });
        })
        // .RequireAuthorization()
        .WithName("CreateConversation")
        .WithTags("Chats")
        .Produces(200)
        .Produces(400)
        .WithSummary("Create a new conversation")
        .WithDescription(@"
            Creates a new conversation or returns existing one (for DIRECT type).

            Conversation Types:
            - DIRECT: 1-1 chat between two users (must have exactly 2 participants)
                     If conversation already exists, returns the existing one
            - GROUP: Group chat with multiple participants
            - TEAM: Team-specific chat
            - INCIDENT: Chat related to a specific incident
            - SHIFT: Chat related to a specific shift

            Request Body:
            {
              ""conversationType"": ""DIRECT"",
              ""participantIds"": [""userId1"", ""userId2""],
              ""conversationName"": ""Optional conversation name"",
              ""shiftId"": ""Optional shift ID"",
              ""incidentId"": ""Optional incident ID"",
              ""teamId"": ""Optional team ID"",
              ""contractId"": ""Optional contract ID""
            }

            Examples:
            POST /api/chats/conversations
            Body: {
              ""conversationType"": ""DIRECT"",
              ""participantIds"": [""550e8400-e29b-41d4-a716-446655440000"", ""550e8400-e29b-41d4-a716-446655440001""]
            }
        ");
    }
}

/// <summary>
/// Request model for creating conversation
/// </summary>
public record CreateConversationRequest
{
    public string ConversationType { get; init; } = string.Empty;
    public List<Guid>? ParticipantIds { get; init; }
    public List<ParticipantInfo>? Participants { get; init; }
    public string? ConversationName { get; init; }
    public Guid? ShiftId { get; init; }
    public Guid? IncidentId { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? ContractId { get; init; }
}
