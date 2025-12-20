namespace Chats.API.Services;

/// <summary>
/// Interface for managing user SignalR connections
/// Quản lý mapping giữa userId và connectionIds (một user có thể có nhiều connections)
/// </summary>
public interface IUserConnectionManager
{
    /// <summary>
    /// Thêm connection cho user
    /// </summary>
    Task AddConnectionAsync(string userId, string connectionId);

    /// <summary>
    /// Remove connection của user
    /// </summary>
    Task RemoveConnectionAsync(string userId, string connectionId);

    /// <summary>
    /// Get tất cả connectionIds của user
    /// </summary>
    Task<List<string>> GetConnectionsAsync(string userId);

    /// <summary>
    /// Check user có connection nào đang active không
    /// </summary>
    Task<bool> HasConnectionsAsync(string userId);

    /// <summary>
    /// Get userId từ connectionId
    /// </summary>
    Task<string?> GetUserIdAsync(string connectionId);

    /// <summary>
    /// Get tất cả users đang online
    /// </summary>
    Task<List<string>> GetOnlineUsersAsync();
}
