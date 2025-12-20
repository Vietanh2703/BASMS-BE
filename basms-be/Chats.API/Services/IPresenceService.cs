namespace Chats.API.Services;

/// <summary>
/// Interface for tracking user presence (online/offline status)
/// </summary>
public interface IPresenceService
{
    /// <summary>
    /// Mark user as connected/online
    /// </summary>
    Task UserConnectedAsync(string userId);

    /// <summary>
    /// Mark user as disconnected/offline
    /// </summary>
    Task UserDisconnectedAsync(string userId);

    /// <summary>
    /// Check if user is online
    /// </summary>
    Task<bool> IsUserOnlineAsync(string userId);

    /// <summary>
    /// Get last seen time for user
    /// </summary>
    Task<DateTime?> GetLastSeenAsync(string userId);

    /// <summary>
    /// Get all online users
    /// </summary>
    Task<List<string>> GetOnlineUsersAsync();

    /// <summary>
    /// Update user's last activity time
    /// </summary>
    Task UpdateLastActivityAsync(string userId);
}
