namespace Chats.API.ChatHandler.GetAllConversations;

/// <summary>
/// Endpoint để lấy danh sách tất cả conversations
/// </summary>
public class GetAllConversationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/chats/conversations/get-all", async (
    HttpContext httpContext,
    ISender sender,
    ILogger<GetAllConversationsEndpoint> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation(
        "GET /api/chats/conversations/get-all - Getting conversations for current user");

    // Get userId from JWT claims
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
              ?? httpContext.User.FindFirst("sub")?.Value
              ?? httpContext.User.FindFirst("userId")?.Value;

    if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var userGuid))
    {
        logger.LogWarning("Unauthorized: Invalid or missing userId in token");
        return Results.Unauthorized();
    }

    var query = new GetAllConversationsQuery(userGuid);

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
        message = "Conversations sorted by last message time (newest first)"
    });
})
        .RequireAuthorization()
        .WithName("GetAllConversations")
        .WithTags("Chats")
        .Produces(200)
        .Produces(400)
        .WithSummary("Get all conversations")
        .WithDescription(@"
            Returns all conversations sorted by last message time (newest first).

            Conversation Types:
            - DIRECT: 1-1 chat between two users
            - GROUP: Group chat with multiple participants
            - TEAM: Team-specific chat
            - INCIDENT: Chat related to a specific incident
            - SHIFT: Chat related to a specific shift

            Examples:
            GET /api/chats/conversations/get-all
        ");
    }
}
