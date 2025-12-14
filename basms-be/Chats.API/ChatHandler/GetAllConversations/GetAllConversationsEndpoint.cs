namespace Chats.API.ChatHandler.GetAllConversations;

/// <summary>
/// Endpoint để lấy danh sách tất cả conversations với filtering
/// </summary>
public class GetAllConversationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/chats/conversations/get-all", async (
    [FromQuery] Guid? userId,
    [FromQuery] string? conversationType,
    [FromQuery] Guid? shiftId,
    [FromQuery] Guid? incidentId,
    [FromQuery] Guid? teamId,
    [FromQuery] Guid? contractId,
    [FromQuery] bool? isActive,
    ISender sender,
    ILogger<GetAllConversationsEndpoint> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation(
        "GET /api/chats/conversations/get-all - Getting all conversations");

    var query = new GetAllConversationsQuery(
        UserId: userId,
        ConversationType: conversationType,
        ShiftId: shiftId,
        IncidentId: incidentId,
        TeamId: teamId,
        ContractId: contractId,
        IsActive: isActive
    );

    var result = await sender.Send(query, cancellationToken);

    if (!result.Success)
    {
        logger.LogWarning(
            "Failed to get conversations: {Error}",
            result.ErrorMessage);

        return Results.BadRequest(new
        {
            success = false,
            error = result.ErrorMessage
        });
    }

    logger.LogInformation(
        "✓ Retrieved {Count} conversations",
        result.Conversations.Count);

    return Results.Ok(new
    {
        success = true,
        data = result.Conversations,
        totalCount = result.TotalCount,
        message = "Conversations sorted by last message time (newest first)",
        filters = new
        {
            userId = userId?.ToString() ?? "all",
            conversationType = conversationType ?? "all",
            shiftId = shiftId?.ToString() ?? "all",
            incidentId = incidentId?.ToString() ?? "all",
            teamId = teamId?.ToString() ?? "all",
            contractId = contractId?.ToString() ?? "all",
            isActive = isActive?.ToString() ?? "all"
        }
    });
})
        // .RequireAuthorization()
        .WithName("GetAllConversations")
        .WithTags("Chats")
        .Produces(200)
        .Produces(400)
        .WithSummary("Get all conversations with filtering")
        .WithDescription(@"
            Returns all conversations sorted by last message time (newest first).

            Conversation Types:
            - DIRECT: 1-1 chat between two users
            - GROUP: Group chat with multiple participants
            - TEAM: Team-specific chat
            - INCIDENT: Chat related to a specific incident
            - SHIFT: Chat related to a specific shift

            Query Parameters:
            - userId (optional but recommended): Get all conversations where this user is a participant
            - conversationType (optional): Filter by conversation type (DIRECT, GROUP, TEAM, INCIDENT, SHIFT)
            - shiftId (optional): Filter conversations related to a specific shift
            - incidentId (optional): Filter conversations related to a specific incident
            - teamId (optional): Filter conversations related to a specific team
            - contractId (optional): Filter conversations related to a specific contract
            - isActive (optional): Filter by active status (true/false)

            Examples:
            GET /api/chats/conversations/get-all?userId={guid}
            GET /api/chats/conversations/get-all?userId={guid}&conversationType=DIRECT
            GET /api/chats/conversations/get-all?shiftId={guid}
            GET /api/chats/conversations/get-all?incidentId={guid}
            GET /api/chats/conversations/get-all?teamId={guid}
            GET /api/chats/conversations/get-all?userId={guid}&isActive=true
        ");
    }
}
