namespace Users.API.UsersHandler.RefreshAccessToken;

// Command để refresh access token
public record RefreshAccessTokenCommand(
    string RefreshToken  // Refresh token từ client
) : ICommand<RefreshAccessTokenResult>;

// Result trả về sau khi refresh thành công
public record RefreshAccessTokenResult(
    Guid UserId,
    string Email,
    string AccessToken,             // JWT access token mới
    string RefreshToken,            // Refresh token mới
    DateTime AccessTokenExpiry,     // Thời điểm access token mới hết hạn
    DateTime RefreshTokenExpiry     // Thời điểm refresh token mới hết hạn
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
        // Bước 1: Validate JWT settings
        ValidateJwtSettings();

        try
        {
            // Bước 2: Tạo kết nối database và transaction
            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Bước 3: Tìm refresh token trong database
                var refreshTokens = await connection.GetAllAsync<RefreshTokens>(transaction);
                var storedToken = refreshTokens.FirstOrDefault(t => 
                    t.Token == command.RefreshToken && !t.IsRevoked);

                if (storedToken == null)
                {
                    logger.LogWarning("Refresh token not found or revoked");
                    throw new UnauthorizedAccessException("Invalid refresh token");
                }

                // Bước 4: Kiểm tra refresh token còn hạn không
                if (storedToken.ExpiresAt <= DateTime.UtcNow)
                {
                    logger.LogWarning("Refresh token expired for UserId: {UserId}", storedToken.UserId);
                    throw new UnauthorizedAccessException("Refresh token expired");
                }

                // Bước 5: Lấy thông tin user
                var user = await connection.GetAsync<Models.Users>(storedToken.UserId, transaction);
                if (user == null || user.IsDeleted)
                {
                    logger.LogWarning("User not found for refresh token");
                    throw new UnauthorizedAccessException("User not found");
                }

                // Bước 6: Kiểm tra user có active không
                if (!user.IsActive)
                {
                    logger.LogWarning("User account inactive for UserId: {UserId}", user.Id);
                    throw new UnauthorizedAccessException("Account is inactive");
                }

                // Bước 7: Tạo JWT tokens mới
                var newAccessToken = GenerateAccessToken(user);
                var newRefreshToken = GenerateRefreshToken();
                
                // Tính toán thời gian hết hạn
                var accessTokenExpiry = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);
                var refreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);

                // Bước 8: Lưu Access Token mới vào bảng user_tokens
                // XÓA tất cả access tokens cũ, chỉ giữ token mới nhất
                await SaveUserTokenAsync(connection, transaction, user.Id, newAccessToken, accessTokenExpiry);

                // Bước 9: Lưu Refresh Token mới vào bảng refresh_tokens
                // XÓA tất cả refresh tokens cũ, chỉ giữ token mới nhất
                await SaveRefreshTokenAsync(connection, transaction, user.Id, newRefreshToken, refreshTokenExpiry);

                // Bước 10: Ghi log audit trail
                await LogAuditAsync(connection, transaction, user.Id, "REFRESH_TOKEN");

                // Bước 11: Commit transaction
                transaction.Commit();

                logger.LogInformation("Access token refreshed successfully for UserId: {UserId}", user.Id);

                // Bước 12: Trả về kết quả với tokens mới
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
                // Rollback nếu có lỗi
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

    // Validate JWT settings
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

    // Tạo JWT Access Token
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

    // Tạo Refresh Token
    private string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    // Lưu Access Token vào database
    private async Task SaveUserTokenAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        // XÓA tất cả access tokens cũ
        var existingTokens = await connection.GetAllAsync<UserTokens>(transaction);
        var userTokens = existingTokens.Where(t => 
            t.UserId == userId && 
            t.TokenType == "access_token").ToList();

        foreach (var existingToken in userTokens)
        {
            await connection.DeleteAsync(existingToken, transaction);
        }

        // INSERT access token mới
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

    // Lưu Refresh Token vào database
    private async Task SaveRefreshTokenAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        // XÓA tất cả refresh tokens cũ
        var existingTokens = await connection.GetAllAsync<RefreshTokens>(transaction);
        var userRefreshTokens = existingTokens.Where(t => 
            t.UserId == userId).ToList();

        foreach (var existingToken in userRefreshTokens)
        {
            await connection.DeleteAsync(existingToken, transaction);
        }

        // INSERT refresh token mới
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

    // Ghi log audit trail
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
