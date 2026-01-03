using System.Data;
using System.Security.Cryptography;
using Dapper.Contrib.Extensions;
using Users.API.Models;

namespace Users.API.Utilities;

/// <summary>
/// Helper methods để quản lý tokens (access tokens, refresh tokens, sessions)
/// </summary>
public static class TokenHelpers
{
    /// <summary>
    /// Generate secure refresh token
    /// </summary>
    public static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Generate random OTP code
    /// </summary>
    public static string GenerateOtpCode(int length = 6)
    {
        var random = new Random();
        var otp = "";
        for (int i = 0; i < length; i++)
        {
            otp += random.Next(0, 10).ToString();
        }
        return otp;
    }

    /// <summary>
    /// Save access token vào database (revoke old tokens)
    /// </summary>
    public static async Task SaveAccessTokenAsync(
        this IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        // Revoke old access tokens
        var existingTokens = await connection.GetAllAsync<UserTokens>(transaction);
        var userTokens = existingTokens.Where(t =>
            t.UserId == userId &&
            t.TokenType == "access_token").ToList();

        foreach (var existingToken in userTokens)
        {
            await connection.DeleteAsync(existingToken, transaction);
        }

        // Insert new token
        var userToken = new UserTokens
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            TokenType = "access_token",
            ExpiresAt = expiresAt,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(userToken, transaction);
    }

    /// <summary>
    /// Save refresh token vào database (revoke old tokens)
    /// </summary>
    public static async Task SaveRefreshTokenAsync(
        this IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        // Revoke old refresh tokens
        var existingTokens = await connection.GetAllAsync<RefreshTokens>(transaction);
        var userRefreshTokens = existingTokens.Where(t => t.UserId == userId).ToList();

        foreach (var existingToken in userRefreshTokens)
        {
            await connection.DeleteAsync(existingToken, transaction);
        }

        // Insert new token
        var refreshToken = new RefreshTokens
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(refreshToken, transaction);
    }

    /// <summary>
    /// Save or update user session
    /// </summary>
    public static async Task SaveOrUpdateSessionAsync(
        this IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        DateTime expiresAt)
    {
        var sessions = await connection.GetAllAsync<UserSessions>(transaction);
        var existingSession = sessions.FirstOrDefault(s => s.UserId == userId && !s.IsDeleted);

        if (existingSession != null)
        {
            // Update existing session
            existingSession.IsActive = true;
            existingSession.ExpiresAt = expiresAt;
            existingSession.LastActivityAt = DateTime.UtcNow;
            existingSession.UpdatedAt = DateTime.UtcNow;
            await connection.UpdateAsync(existingSession, transaction);
        }
        else
        {
            // Create new session
            var session = new UserSessions
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SessionToken = Guid.NewGuid().ToString(),
                ExpiresAt = expiresAt,
                IsActive = true,
                IsDeleted = false,
                LastActivityAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await connection.InsertAsync(session, transaction);
        }
    }

    /// <summary>
    /// Revoke tất cả tokens của user (for logout)
    /// </summary>
    public static async Task RevokeAllUserTokensAsync(
        this IDbConnection connection,
        IDbTransaction transaction,
        Guid userId)
    {
        // Revoke access tokens
        var accessTokens = await connection.GetAllAsync<UserTokens>(transaction);
        var userAccessTokens = accessTokens.Where(t => t.UserId == userId).ToList();

        foreach (var token in userAccessTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.UpdatedAt = DateTime.UtcNow;
            await connection.UpdateAsync(token, transaction);
        }

        // Revoke refresh tokens
        var refreshTokens = await connection.GetAllAsync<RefreshTokens>(transaction);
        var userRefreshTokens = refreshTokens.Where(t => t.UserId == userId).ToList();

        foreach (var token in userRefreshTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.UpdatedAt = DateTime.UtcNow;
            await connection.UpdateAsync(token, transaction);
        }

        // Deactivate sessions
        var sessions = await connection.GetAllAsync<UserSessions>(transaction);
        var userSessions = sessions.Where(s => s.UserId == userId && !s.IsDeleted).ToList();

        foreach (var session in userSessions)
        {
            session.IsActive = false;
            session.UpdatedAt = DateTime.UtcNow;
            await connection.UpdateAsync(session, transaction);
        }
    }

    /// <summary>
    /// Validate refresh token
    /// </summary>
    public static async Task<RefreshTokens?> GetValidRefreshTokenAsync(
        this IDbConnection connection,
        string token,
        IDbTransaction? transaction = null)
    {
        var tokens = await connection.GetAllAsync<RefreshTokens>(transaction);
        var refreshToken = tokens.FirstOrDefault(t =>
            t.Token == token &&
            !t.IsRevoked &&
            t.ExpiresAt > DateTime.UtcNow);

        return refreshToken;
    }
}
