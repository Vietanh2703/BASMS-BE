using System.Collections.Concurrent;

namespace Chats.API.Services;

/// <summary>
/// In-memory implementation of user connection manager
/// Trong production nên dùng Redis để support multi-server (SignalR backplane)
/// </summary>
public class UserConnectionManager : IUserConnectionManager
{
    // userId -> List<connectionId>
    private readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new();

    // connectionId -> userId (for reverse lookup)
    private readonly ConcurrentDictionary<string, string> _connectionUsers = new();

    private readonly ILogger<UserConnectionManager> _logger;

    public UserConnectionManager(ILogger<UserConnectionManager> logger)
    {
        _logger = logger;
    }

    public Task AddConnectionAsync(string userId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(connectionId))
        {
            _logger.LogWarning("Attempted to add invalid connection: userId={UserId}, connectionId={ConnectionId}",
                userId, connectionId);
            return Task.CompletedTask;
        }

        // Add to userId -> connectionIds mapping
        _userConnections.AddOrUpdate(
            userId,
            new HashSet<string> { connectionId },
            (key, existingSet) =>
            {
                lock (existingSet)
                {
                    existingSet.Add(connectionId);
                    return existingSet;
                }
            });

        // Add to connectionId -> userId mapping
        _connectionUsers.TryAdd(connectionId, userId);

        _logger.LogInformation(
            "User {UserId} connected with connectionId {ConnectionId}. Total connections: {Count}",
            userId, connectionId, _userConnections[userId].Count);

        return Task.CompletedTask;
    }

    public Task RemoveConnectionAsync(string userId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(connectionId))
        {
            return Task.CompletedTask;
        }

        // Remove from userId -> connectionIds mapping
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                connections.Remove(connectionId);

                // If no more connections, remove user entry
                if (connections.Count == 0)
                {
                    _userConnections.TryRemove(userId, out _);
                    _logger.LogInformation("User {UserId} has no more connections (offline)", userId);
                }
                else
                {
                    _logger.LogInformation(
                        "User {UserId} disconnected connectionId {ConnectionId}. Remaining connections: {Count}",
                        userId, connectionId, connections.Count);
                }
            }
        }

        // Remove from connectionId -> userId mapping
        _connectionUsers.TryRemove(connectionId, out _);

        return Task.CompletedTask;
    }

    public Task<List<string>> GetConnectionsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(new List<string>());
        }

        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                return Task.FromResult(connections.ToList());
            }
        }

        return Task.FromResult(new List<string>());
    }

    public Task<bool> HasConnectionsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(false);
        }

        var hasConnections = _userConnections.TryGetValue(userId, out var connections) && connections.Count > 0;
        return Task.FromResult(hasConnections);
    }

    public Task<string?> GetUserIdAsync(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return Task.FromResult<string?>(null);
        }

        _connectionUsers.TryGetValue(connectionId, out var userId);
        return Task.FromResult(userId);
    }

    public Task<List<string>> GetOnlineUsersAsync()
    {
        var onlineUsers = _userConnections.Keys.ToList();
        return Task.FromResult(onlineUsers);
    }
}
