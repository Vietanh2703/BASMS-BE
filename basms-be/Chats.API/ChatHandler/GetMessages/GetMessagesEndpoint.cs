namespace Chats.API.ChatHandler.GetMessages;

/// <summary>
/// Endpoint để lấy danh sách messages của conversation
/// </summary>
public class GetMessagesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/chats/conversations/{conversationId}/messages", async (
            Guid conversationId,
            HttpContext httpContext,
            ISender sender,
            ILogger<GetMessagesEndpoint> logger,
            int limit = 50,
            Guid? beforeMessageId = null,
            CancellationToken cancellationToken = default) =>
        {
            logger.LogInformation(
                "GET /api/chats/conversations/{ConversationId}/messages - limit={Limit}, beforeMessageId={BeforeMessageId}",
                conversationId, limit, beforeMessageId);

            // Get userId from JWT claims
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? httpContext.User.FindFirst("sub")?.Value
                      ?? httpContext.User.FindFirst("userId")?.Value;

            if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                logger.LogWarning("Unauthorized: Invalid or missing userId in token");
                return Results.Unauthorized();
            }

            // Validate limit
            if (limit < 1 || limit > 100)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    error = "Limit must be between 1 and 100"
                });
            }

            // Create query
            var query = new GetMessagesQuery(
                ConversationId: conversationId,
                UserId: userGuid,
                Limit: limit,
                BeforeMessageId: beforeMessageId
            );

            // Execute
            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning("Failed to get messages: {Error}", result.ErrorMessage);
                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "✓ Retrieved {Count} messages for conversation {ConversationId}",
                result.Messages.Count, conversationId);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    messages = result.Messages,
                    totalCount = result.TotalCount,
                    hasMore = result.HasMore,
                    oldestMessageId = result.OldestMessageId
                },
                message = $"Retrieved {result.Messages.Count} messages"
            });
        })
        .RequireAuthorization()
        .WithName("GetMessages")
        .WithTags("Chats - Messages")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .WithSummary("Get messages from a conversation")
        .WithDescription(@"
            Get messages from a conversation with pagination support.

            Pagination:
            - Initial load: GET /api/chats/conversations/{id}/messages?limit=50
            - Load more (older): GET /api/chats/conversations/{id}/messages?limit=50&beforeMessageId={oldestMessageId}

            Features:
            - Cursor-based pagination (efficient for large datasets)
            - Messages sorted by CreatedAt DESC (newest first)
            - Returns hasMore flag to indicate if there are older messages
            - Returns oldestMessageId for next page cursor
            - Validates user is participant

            Query Parameters:
            - limit: Number of messages to retrieve (1-100, default: 50)
            - beforeMessageId: Cursor for pagination (optional)

            Response:
            {
                ""messages"": [...],
                ""totalCount"": 1234,
                ""hasMore"": true,
                ""oldestMessageId"": ""guid""
            }

            Infinite Scroll Example:
            1. Initial: GET ?limit=50 → Returns 50 newest messages
            2. Scroll up: GET ?limit=50&beforeMessageId={oldestMessageId} → Returns 50 older messages
            3. Repeat until hasMore = false
        ");
    }
}
