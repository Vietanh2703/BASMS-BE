using Microsoft.AspNetCore.SignalR;
using Chats.API.Services;
using System.Security.Claims;

namespace Chats.API.Hubs;

/// <summary>
/// SignalR Hub chính cho real-time chat
/// Xử lý connections, messages, typing indicators, presence
/// </summary>
[Authorize]
public class ChatHub : Hub<IChatsClient>
{
    private readonly IUserConnectionManager _connectionManager;
    private readonly IPresenceService _presenceService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IUserConnectionManager connectionManager,
        IPresenceService presenceService,
        ILogger<ChatHub> logger)
    {
        _connectionManager = connectionManager;
        _presenceService = presenceService;
        _logger = logger;
    }

    // ============================================================================
    // CONNECTION LIFECYCLE
    // ============================================================================

    /// <summary>
    /// Được gọi khi client kết nối
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("User connected without valid userId. ConnectionId: {ConnectionId}",
                Context.ConnectionId);
            await base.OnConnectedAsync();
            return;
        }

        _logger.LogInformation(
            "User {UserId} connecting with ConnectionId {ConnectionId}",
            userId, Context.ConnectionId);

        try
        {
            // Track connection
            await _connectionManager.AddConnectionAsync(userId, Context.ConnectionId);

            // Update presence
            await _presenceService.UserConnectedAsync(userId);

            // Notify other users that this user is online
            await Clients.Others.UserOnline(userId);

            _logger.LogInformation(
                "✓ User {UserId} connected successfully. ConnectionId: {ConnectionId}",
                userId, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error during connection for user {UserId}, ConnectionId {ConnectionId}",
                userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Được gọi khi client ngắt kết nối
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            await base.OnDisconnectedAsync(exception);
            return;
        }

        _logger.LogInformation(
            "User {UserId} disconnecting. ConnectionId: {ConnectionId}",
            userId, Context.ConnectionId);

        try
        {
            // Remove connection
            await _connectionManager.RemoveConnectionAsync(userId, Context.ConnectionId);

            // Check if user still has other active connections
            var hasConnections = await _connectionManager.HasConnectionsAsync(userId);

            if (!hasConnections)
            {
                // User is completely offline
                await _presenceService.UserDisconnectedAsync(userId);

                // Notify other users with Vietnam time
                var lastSeen = DateTime.UtcNow.ToVietnamTime();
                await Clients.Others.UserOffline(userId, lastSeen);

                _logger.LogInformation(
                    "✓ User {UserId} is now OFFLINE (no more connections). LastSeen: {LastSeen}",
                    userId, lastSeen);
            }
            else
            {
                _logger.LogInformation(
                    "User {UserId} still has other active connections, remains ONLINE",
                    userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error during disconnection for user {UserId}, ConnectionId {ConnectionId}",
                userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ============================================================================
    // CONVERSATION GROUPS
    // ============================================================================

    /// <summary>
    /// Join conversation group để nhận real-time updates
    /// Client phải gọi method này khi mở conversation
    /// </summary>
    public async Task JoinConversation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            _logger.LogWarning("Attempted to join invalid conversationId");
            return;
        }

        var userId = GetUserId();

        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);

            _logger.LogInformation(
                "User {UserId} joined conversation {ConversationId}. ConnectionId: {ConnectionId}",
                userId, conversationId, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error joining conversation {ConversationId} for user {UserId}",
                conversationId, userId);
        }
    }

    /// <summary>
    /// Leave conversation group
    /// Client phải gọi method này khi đóng conversation
    /// </summary>
    public async Task LeaveConversation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            _logger.LogWarning("Attempted to leave invalid conversationId");
            return;
        }

        var userId = GetUserId();

        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);

            _logger.LogInformation(
                "User {UserId} left conversation {ConversationId}. ConnectionId: {ConnectionId}",
                userId, conversationId, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error leaving conversation {ConversationId} for user {UserId}",
                conversationId, userId);
        }
    }

    // ============================================================================
    // TYPING INDICATORS
    // ============================================================================

    /// <summary>
    /// User bắt đầu typing
    /// Client gọi khi user bắt đầu gõ phím
    /// </summary>
    public async Task SendTypingIndicator(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return;
        }

        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        try
        {
            // Broadcast to others in the conversation (exclude sender)
            await Clients.OthersInGroup(conversationId)
                .UserIsTyping(userId, conversationId);

            _logger.LogDebug(
                "User {UserId} is typing in conversation {ConversationId}",
                userId, conversationId);

            // Auto-stop typing after 3 seconds
            _ = Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(async _ =>
            {
                await Clients.OthersInGroup(conversationId)
                    .UserStoppedTyping(userId, conversationId);

                _logger.LogDebug(
                    "Auto-stopped typing indicator for user {UserId} in conversation {ConversationId}",
                    userId, conversationId);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending typing indicator for user {UserId} in conversation {ConversationId}",
                userId, conversationId);
        }
    }

    /// <summary>
    /// User dừng typing
    /// Client gọi khi user dừng gõ phím hoặc gửi message
    /// </summary>
    public async Task StopTypingIndicator(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return;
        }

        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        try
        {
            await Clients.OthersInGroup(conversationId)
                .UserStoppedTyping(userId, conversationId);

            _logger.LogDebug(
                "User {UserId} stopped typing in conversation {ConversationId}",
                userId, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error stopping typing indicator for user {UserId} in conversation {ConversationId}",
                userId, conversationId);
        }
    }

    // ============================================================================
    // PRESENCE UPDATES
    // ============================================================================

    /// <summary>
    /// Update user's last activity time
    /// Client có thể gọi periodically để update presence
    /// </summary>
    public async Task UpdatePresence()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        try
        {
            await _presenceService.UpdateLastActivityAsync(userId);

            _logger.LogDebug("Updated presence for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating presence for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Get online status của một user
    /// </summary>
    public async Task<bool> IsUserOnline(string targetUserId)
    {
        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            return false;
        }

        try
        {
            return await _presenceService.IsUserOnlineAsync(targetUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking online status for user {UserId}", targetUserId);
            return false;
        }
    }

    // ============================================================================
    // HELPER METHODS
    // ============================================================================

    /// <summary>
    /// Get userId from JWT claims
    /// </summary>
    private string? GetUserId()
    {
        // Try getting from NameIdentifier claim (standard)
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Fallback to "sub" claim (JWT standard)
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = Context.User?.FindFirst("sub")?.Value;
        }

        // Fallback to "userId" claim (custom)
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = Context.User?.FindFirst("userId")?.Value;
        }

        // Last fallback: use Context.UserIdentifier (SignalR's built-in)
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = Context.UserIdentifier;
        }

        return userId;
    }

    /// <summary>
    /// Get user name from JWT claims
    /// </summary>
    private string? GetUserName()
    {
        return Context.User?.FindFirst(ClaimTypes.Name)?.Value
            ?? Context.User?.FindFirst("name")?.Value;
    }
}
