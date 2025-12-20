using System.Collections.Concurrent;

namespace Chats.API.Services;

/// <summary>
/// In-memory implementation of presence tracking
/// Trong production nên dùng Redis hoặc database để persist data
/// </summary>
public class PresenceService : IPresenceService
{
    // userId -> lastSeen timestamp
    private readonly ConcurrentDictionary<string, DateTime> _userPresence = new();

    // userId -> isOnline status
    private readonly ConcurrentDictionary<string, bool> _onlineStatus = new();

    private readonly ILogger<PresenceService> _logger;
    private readonly IDbConnectionFactory _dbFactory;

    public PresenceService(
        ILogger<PresenceService> logger,
        IDbConnectionFactory dbFactory)
    {
        _logger = logger;
        _dbFactory = dbFactory;
    }

    public async Task UserConnectedAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Attempted to mark invalid userId as connected");
            return;
        }

        var now = DateTime.UtcNow;
        _onlineStatus[userId] = true;
        _userPresence[userId] = now;

        _logger.LogInformation("User {UserId} is now ONLINE at {Time}", userId, now);

        // Optional: Update database
        await UpdatePresenceInDatabaseAsync(userId, isOnline: true, lastSeen: now);
    }

    public async Task UserDisconnectedAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Attempted to mark invalid userId as disconnected");
            return;
        }

        var now = DateTime.UtcNow;
        _onlineStatus[userId] = false;
        _userPresence[userId] = now;

        _logger.LogInformation("User {UserId} is now OFFLINE at {Time}", userId, now);

        // Optional: Update database
        await UpdatePresenceInDatabaseAsync(userId, isOnline: false, lastSeen: now);
    }

    public Task<bool> IsUserOnlineAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(false);
        }

        var isOnline = _onlineStatus.TryGetValue(userId, out var status) && status;
        return Task.FromResult(isOnline);
    }

    public Task<DateTime?> GetLastSeenAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult<DateTime?>(null);
        }

        if (_userPresence.TryGetValue(userId, out var lastSeen))
        {
            return Task.FromResult<DateTime?>(lastSeen);
        }

        return Task.FromResult<DateTime?>(null);
    }

    public Task<List<string>> GetOnlineUsersAsync()
    {
        var onlineUsers = _onlineStatus
            .Where(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();

        return Task.FromResult(onlineUsers);
    }

    public async Task UpdateLastActivityAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var now = DateTime.UtcNow;
        _userPresence[userId] = now;

        // Optional: Update database periodically (not every activity)
        // Có thể implement batch update sau 1-5 phút
        _logger.LogDebug("Updated last activity for user {UserId} at {Time}", userId, now);
    }

    /// <summary>
    /// Update presence status in database
    /// Tạo bảng user_presence nếu muốn persist data
    /// </summary>
    private async Task UpdatePresenceInDatabaseAsync(string userId, bool isOnline, DateTime lastSeen)
    {
        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            // Check if user_presence table exists, if not skip
            var tableExists = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = DATABASE()
                AND table_name = 'user_presence'
            ");

            if (tableExists == 0)
            {
                // Table doesn't exist, skip database update
                // Sẽ tạo table này trong phase sau nếu cần
                return;
            }

            // Upsert presence data
            var sql = @"
                INSERT INTO user_presence (UserId, IsOnline, LastSeen, UpdatedAt)
                VALUES (@UserId, @IsOnline, @LastSeen, @UpdatedAt)
                ON DUPLICATE KEY UPDATE
                    IsOnline = @IsOnline,
                    LastSeen = @LastSeen,
                    UpdatedAt = @UpdatedAt";

            await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                IsOnline = isOnline,
                LastSeen = lastSeen,
                UpdatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            // Don't fail if database update fails
            _logger.LogWarning(ex, "Failed to update presence in database for user {UserId}", userId);
        }
    }
}
