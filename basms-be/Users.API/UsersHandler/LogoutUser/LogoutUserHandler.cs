using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Users.API.Extensions;

namespace Users.API.UsersHandler.LogoutUser;

public record LogoutUserCommand : ICommand<LogoutUserResult>;

public record LogoutUserResult(bool Success, string Message);

public class LogoutUserHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<LogoutUserHandler> logger,
    IHttpContextAccessor httpContextAccessor)
    : ICommandHandler<LogoutUserCommand, LogoutUserResult>
{
    public async Task<LogoutUserResult> Handle(LogoutUserCommand command, CancellationToken cancellationToken)
    {
        try
        {
            // Get userId from JWT token
            var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }

            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Check if user has any active tokens
                var userTokens = await connection.GetAllAsync<UserTokens>(transaction);
                var userAccessTokens = userTokens.Where(t => 
                    t.UserId == userId).ToList();

                var refreshTokens = await connection.GetAllAsync<RefreshTokens>(transaction);
                var userRefreshTokens = refreshTokens.Where(t => 
                    t.UserId == userId).ToList();

                // If no tokens exist, user is already logged out
                if (!userAccessTokens.Any() && !userRefreshTokens.Any())
                {
                    logger.LogWarning("User already logged out: UserId: {UserId}", userId);
                    throw new InvalidOperationException("User is already logged out");
                }

                // DELETE all access tokens
                foreach (var token in userAccessTokens)
                {
                    await connection.DeleteAsync(token, transaction);
                }

                // DELETE all refresh tokens
                foreach (var token in userRefreshTokens)
                {
                    await connection.DeleteAsync(token, transaction);
                }

                // Deactivate user session
                var sessions = await connection.GetAllAsync<UserSessions>(transaction);
                var activeSession = sessions.FirstOrDefault(s => 
                    s.UserId == userId && 
                    s.IsActive && 
                    !s.IsDeleted);

                if (activeSession != null)
                {
                    activeSession.IsActive = false;
                    activeSession.UpdatedAt = DateTime.UtcNow;
                    await connection.UpdateAsync(activeSession, transaction);
                }

                // Log audit trail
                await LogAuditAsync(connection, transaction, userId, "LOGOUT");

                transaction.Commit();

                logger.LogInformation("User logged out successfully: UserId: {UserId}", userId);

                return new LogoutUserResult(true, "Logged out successfully");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during logout");
            throw;
        }
    }

    private async Task LogAuditAsync(IDbConnection connection, IDbTransaction transaction, Guid userId, string action)
    {
        var auditLog = new AuditLogs
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityType = "User",
            EntityId = userId,
            Status = "success",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(auditLog, transaction);
    }
}