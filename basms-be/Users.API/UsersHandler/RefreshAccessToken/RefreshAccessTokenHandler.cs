using BuildingBlocks.Exceptions;

namespace Users.API.UsersHandler.RefreshAccessToken;


public record RefreshAccessTokenCommand(
    string RefreshToken 
) : ICommand<RefreshAccessTokenResult>;


public record RefreshAccessTokenResult(
    Guid UserId,
    string Email,
    string AccessToken,            
    string RefreshToken,           
    DateTime AccessTokenExpiry,     
    DateTime RefreshTokenExpiry    
);

public class RefreshAccessTokenHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<RefreshAccessTokenHandler> logger,
    IOptions<JwtSettings> jwtSettings)
    : ICommandHandler<RefreshAccessTokenCommand, RefreshAccessTokenResult>
{
    private readonly JwtSettings _jwtSettings = jwtSettings.Value;

    public async Task<RefreshAccessTokenResult> Handle(RefreshAccessTokenCommand command, CancellationToken cancellationToken)
    {
        ValidateJwtSettings();

        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var sql = @"
                    SELECT * FROM refresh_tokens
                    WHERE Token = @Token
                    AND IsRevoked = 0
                    AND IsDeleted = 0
                    LIMIT 1";

                var storedToken = await connection.QueryFirstOrDefaultAsync<RefreshTokens>(
                    sql,
                    new { Token = command.RefreshToken },
                    transaction);

                if (storedToken == null)
                {
                    logger.LogWarning("Refresh token not found or revoked");
                    throw new UnauthorizedException("Invalid refresh token", "AUTH_INVALID_REFRESH_TOKEN");
                }
                
                if (storedToken.ExpiresAt <= DateTime.UtcNow)
                {
                    logger.LogWarning("Refresh token expired for UserId: {UserId}", storedToken.UserId);
                    throw new UnauthorizedException("Refresh token expired", "AUTH_REFRESH_TOKEN_EXPIRED");
                }
                
                var user = await connection.GetAsync<Models.Users>(storedToken.UserId, transaction);
                if (user == null || user.IsDeleted)
                {
                    logger.LogWarning("User not found for refresh token");
                    throw new NotFoundException("User not found", "USER_NOT_FOUND");
                }
                
                if (!user.IsActive)
                {
                    logger.LogWarning("User account inactive for UserId: {UserId}", user.Id);
                    throw new UnauthorizedException("Account is inactive", "AUTH_ACCOUNT_INACTIVE");
                }
                
                var newAccessToken = GenerateAccessToken(user);
                var newRefreshToken = GenerateRefreshToken();
                
                var accessTokenExpiry = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);
                var refreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
                
                await SaveUserTokenAsync(connection, transaction, user.Id, newAccessToken, accessTokenExpiry);
                
                await SaveRefreshTokenAsync(connection, transaction, user.Id, newRefreshToken, refreshTokenExpiry);
                
                await LogAuditAsync(connection, transaction, user.Id, "REFRESH_TOKEN");
                
                transaction.Commit();

                logger.LogInformation("Access token refreshed successfully for UserId: {UserId}", user.Id);
                
                return new RefreshAccessTokenResult(
                    UserId: user.Id,
                    Email: user.Email,
                    AccessToken: newAccessToken,
                    RefreshToken: newRefreshToken,
                    AccessTokenExpiry: accessTokenExpiry,
                    RefreshTokenExpiry: refreshTokenExpiry
                );
            }
            catch
            {
                transaction.Rollback();
                logger.LogWarning("Transaction rolled back due to error");
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing access token");
            throw;
        }
    }
    
    private void ValidateJwtSettings()
    {
        if (_jwtSettings == null)
        {
            throw new InvalidOperationException("JWT settings are not configured");
        }

        if (string.IsNullOrEmpty(_jwtSettings.SecretKey))
        {
            throw new InvalidOperationException("JWT SecretKey is not configured");
        }

        if (_jwtSettings.SecretKey.Length < 32)
        {
            throw new InvalidOperationException("JWT SecretKey must be at least 32 characters long");
        }

        if (string.IsNullOrEmpty(_jwtSettings.Issuer))
        {
            throw new InvalidOperationException("JWT Issuer is not configured");
        }

        if (string.IsNullOrEmpty(_jwtSettings.Audience))
        {
            throw new InvalidOperationException("JWT Audience is not configured");
        }
    }
    
    private string GenerateAccessToken(Models.Users user)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(ClaimTypes.Role, user.RoleId.ToString()),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new("userId", user.Id.ToString()),
                new("roleId", user.RoleId.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating access token");
            throw;
        }
    }
    
    private string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
    
    private async Task SaveUserTokenAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        var deleteSql = @"
            DELETE FROM user_tokens
            WHERE UserId = @UserId
            AND TokenType = 'access_token'";

        await connection.ExecuteAsync(deleteSql, new { UserId = userId }, transaction);
        
        var insertSql = @"
            INSERT INTO user_tokens
            (Id, UserId, Token, TokenType, ExpiresAt, IsRevoked, CreatedAt, UpdatedAt, IsDeleted)
            VALUES
            (@Id, @UserId, @Token, @TokenType, @ExpiresAt, @IsRevoked, @CreatedAt, @UpdatedAt, 0)";

        await connection.ExecuteAsync(insertSql, new
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            TokenType = "access_token",
            ExpiresAt = expiresAt,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }, transaction);
    }
    
    private async Task SaveRefreshTokenAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        var deleteSql = @"
            DELETE FROM refresh_tokens
            WHERE UserId = @UserId";

        await connection.ExecuteAsync(deleteSql, new { UserId = userId }, transaction);
        
        var insertSql = @"
            INSERT INTO refresh_tokens
            (Id, UserId, Token, ExpiresAt, IsRevoked, CreatedAt, UpdatedAt, IsDeleted)
            VALUES
            (@Id, @UserId, @Token, @ExpiresAt, @IsRevoked, @CreatedAt, @UpdatedAt, 0)";

        await connection.ExecuteAsync(insertSql, new
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }, transaction);
    }


    private async Task LogAuditAsync(IDbConnection connection, IDbTransaction transaction, Guid userId, string action)
    {
        var auditLog = new AuditLogs
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityType = "Token",
            EntityId = userId,
            Status = "success",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(auditLog, transaction);
    }
}
