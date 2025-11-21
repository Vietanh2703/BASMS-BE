using BuildingBlocks.Exceptions;

namespace Users.API.UsersHandler.LoginUser;

// Command để đăng nhập - hỗ trợ cả email/password và Google
public record LoginUserCommand(
    string Email,                   // Email đăng nhập
    string? Password = null,        // Password (cho email login)
    string? GoogleIdToken = null    // Google ID Token (cho Google login)
) : ICommand<LoginUserResult>;

// Result trả về sau khi đăng nhập thành công
// Chứa tokens và thông tin user cơ bản
public record LoginUserResult(
    Guid UserId,                    // ID user trong database
    string Email,                   // Email của user
    string FullName,                // Tên đầy đủ
    Guid RoleId,                    // ID vai trò
    string AccessToken,             // JWT access token (dùng cho API requests)
    string RefreshToken,            // Refresh token (dùng để lấy access token mới)
    DateTime AccessTokenExpiry,     // Thời điểm access token hết hạn
    DateTime RefreshTokenExpiry,    // Thời điểm refresh token hết hạn
    DateTime SessionExpiry          // Thời điểm session hết hạn
);

public class LoginUserHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<LoginUserHandler> logger,
    LoginUserValidator validator,              // Validator để kiểm tra input
    IOptions<JwtSettings> jwtSettings)          // JWT settings từ appsettings.json
    : ICommandHandler<LoginUserCommand, LoginUserResult>
{
    private readonly JwtSettings _jwtSettings = jwtSettings.Value;

    public async Task<LoginUserResult> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        // Bước 1: Validate JWT settings từ appsettings.json
        // Đảm bảo SecretKey, Issuer, Audience đều được cấu hình đúng
        ValidateJwtSettings();

        // Bước 2: Validate command input
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var validationErrors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );
            throw new BuildingBlocks.Exceptions.ValidationException(validationErrors, "LOGIN_VALIDATION_ERROR");
        }

        try
        {
            // Bước 3: Tạo kết nối database và transaction
            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                Models.Users user;

                // Bước 4: Xác định phương thức đăng nhập (Google hoặc Email/Password)
                if (!string.IsNullOrEmpty(command.GoogleIdToken))
                {
                    // Đăng nhập bằng Google
                    // Verify Google token, tìm hoặc tạo user mới
                    user = await AuthenticateWithGoogleAsync(connection, transaction, command.GoogleIdToken);
                }
                else
                {
                    // Đăng nhập bằng Email/Password
                    // Tìm user và verify password với BCrypt
                    user = await AuthenticateWithEmailPasswordAsync(connection, transaction, command.Email, command.Password!);
                }

                // Bước 5: Verify user trên Firebase Authentication
                // Đảm bảo tài khoản Firebase không bị disabled
                await VerifyFirebaseUserAsync(user.FirebaseUid);

                // Bước 6: Tạo JWT tokens
                // Access Token: Chứa userId, email, roleId - dùng cho API requests
                var accessToken = GenerateAccessToken(user);
                // Refresh Token: Random string - dùng để lấy access token mới
                var refreshToken = GenerateRefreshToken();
                
                // Tính toán thời gian hết hạn
                var accessTokenExpiry = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);
                var refreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
                var sessionExpiry = DateTime.UtcNow.AddDays(30);

                // Bước 7: Lưu Access Token vào bảng user_tokens
                // XÓA tất cả access tokens cũ, chỉ giữ token mới nhất
                await SaveUserTokenAsync(connection, transaction, user.Id, accessToken, accessTokenExpiry);

                // Bước 8: Lưu Refresh Token vào bảng refresh_tokens
                // XÓA tất cả refresh tokens cũ, chỉ giữ token mới nhất
                await SaveRefreshTokenAsync(connection, transaction, user.Id, refreshToken, refreshTokenExpiry);

                // Bước 9: Tạo hoặc cập nhật Session trong bảng user_sessions
                // Reactivate session nếu đã tồn tại
                await SaveUserSessionAsync(connection, transaction, user.Id, sessionExpiry);

                // Bước 10: Cập nhật thông tin đăng nhập cuối
                // Update LastLoginAt và tăng LoginCount
                await UpdateLastLoginAsync(connection, transaction, user.Id);

                // Bước 11: Ghi log audit trail
                await LogAuditAsync(connection, transaction, user.Id, "LOGIN");

                // Bước 12: Commit transaction
                transaction.Commit();

                logger.LogInformation("User logged in successfully: {Email}, UserId: {UserId}", user.Email, user.Id);

                // Bước 13: Trả về kết quả với tokens và thông tin user
                return new LoginUserResult(
                    UserId: user.Id,
                    Email: user.Email,
                    FullName: user.FullName,
                    RoleId: user.RoleId,
                    AccessToken: accessToken,
                    RefreshToken: refreshToken,
                    AccessTokenExpiry: accessTokenExpiry,
                    RefreshTokenExpiry: refreshTokenExpiry,
                    SessionExpiry: sessionExpiry
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
            logger.LogError(ex, "Error during login");
            throw;
        }
    }

    // Hàm validate cấu hình JWT từ appsettings.json
    // Kiểm tra SecretKey, Issuer, Audience đều có giá trị hợp lệ
    private void ValidateJwtSettings()
    {
        if (_jwtSettings == null)
        {
            throw new InvalidOperationException("JWT settings are not configured");
        }

        if (string.IsNullOrEmpty(_jwtSettings.SecretKey))
        {
            throw new InvalidOperationException("JWT SecretKey is not configured in appsettings.json");
        }

        // SecretKey phải ít nhất 32 ký tự (256 bits) để đảm bảo bảo mật
        if (_jwtSettings.SecretKey.Length < 32)
        {
            throw new InvalidOperationException("JWT SecretKey must be at least 32 characters long (256 bits)");
        }

        if (string.IsNullOrEmpty(_jwtSettings.Issuer))
        {
            throw new InvalidOperationException("JWT Issuer is not configured");
        }

        if (string.IsNullOrEmpty(_jwtSettings.Audience))
        {
            throw new InvalidOperationException("JWT Audience is not configured");
        }

        logger.LogDebug("JWT Settings validated successfully");
    }

    // Hàm xác thực user bằng Email và Password
    // Tìm user trong database và verify password với BCrypt
    private async Task<Models.Users> AuthenticateWithEmailPasswordAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string email,
        string password)
    {
        // Bước 1: Tìm user theo email
        var users = await connection.GetAllAsync<Models.Users>(transaction);
        var user = users.FirstOrDefault(u => u.Email == email && !u.IsDeleted);

        if (user == null)
        {
            logger.LogWarning("Login failed: User not found with email {Email}", email);
            throw new UnauthorizedException("Invalid email or password", "AUTH_INVALID_CREDENTIALS");
        }

        // Bước 2: Kiểm tra account active
        if (!user.IsActive)
        {
            logger.LogWarning("Login failed: User account inactive for email {Email}", email);
            throw new UnauthorizedException("Account is inactive. Please contact support.", "AUTH_ACCOUNT_INACTIVE");
        }

        // Bước 3: Kiểm tra user có password không
        if (string.IsNullOrEmpty(user.Password))
        {
            logger.LogWarning("Login failed: No password set for user {Email}. User may have registered with Google.", email);
            throw new UnauthorizedException("This account was registered with Google. Please use Google sign-in.", "AUTH_USE_GOOGLE_LOGIN");
        }

        // Bước 4: Verify password với BCrypt
        try
        {
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.Password);
            if (!isPasswordValid)
            {
                logger.LogWarning("Login failed: Invalid password for user {Email}", email);
                throw new UnauthorizedException("Invalid password. Please check your password and try again.", "AUTH_INVALID_PASSWORD");
            }
        }
        catch (BCrypt.Net.SaltParseException ex)
        {
            logger.LogError(ex, "Login failed: Password hash corrupted for user {Email}", email);
            throw new UnauthorizedException("Account error. Please contact support.", "AUTH_ACCOUNT_ERROR");
        }

        logger.LogInformation("User authenticated successfully with email/password: {Email}", email);
        return user;
    }

    // Hàm xác thực user bằng Google ID Token
    // Verify token với Firebase, tìm hoặc tạo user mới
    private async Task<Models.Users> AuthenticateWithGoogleAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string googleIdToken)
    {
        try
        {
            // Bước 1: Verify Google ID Token với Firebase
            // Firebase sẽ validate token và trả về thông tin user
            var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(googleIdToken);
            var firebaseUid = decodedToken.Uid;
            var email = decodedToken.Claims.ContainsKey("email") ? decodedToken.Claims["email"].ToString() : null;

            if (string.IsNullOrEmpty(email))
            {
                throw new UnauthorizedException("Email not found in Google token", "AUTH_GOOGLE_EMAIL_MISSING");
            }

            // Bước 2: Tìm user theo Firebase UID hoặc email
            var users = await connection.GetAllAsync<Models.Users>(transaction);
            var user = users.FirstOrDefault(u => 
                (u.FirebaseUid == firebaseUid || u.Email == email) && !u.IsDeleted);

            if (user == null)
            {
                // Bước 3: Auto-create user nếu chưa tồn tại (Google sign-up)
                // Tạo user mới với thông tin từ Google
                user = await CreateGoogleUserAsync(connection, transaction, decodedToken);
            }
            else if (user.FirebaseUid != firebaseUid)
            {
                // Bước 4: Update Firebase UID nếu thay đổi
                // User có thể đã đăng ký bằng email trước đó
                user.FirebaseUid = firebaseUid;
                user.AuthProvider = "google";
                user.UpdatedAt = DateTime.UtcNow;
                await connection.UpdateAsync(user, transaction);
            }

            // Bước 5: Kiểm tra account có active không
            if (!user.IsActive)
            {
                throw new UnauthorizedException("Account is inactive", "AUTH_ACCOUNT_INACTIVE");
            }

            return user;
        }
        catch (FirebaseAuthException ex)
        {
            logger.LogError(ex, "Firebase authentication failed");
            throw new UnauthorizedException("Invalid Google token", ex, "AUTH_GOOGLE_TOKEN_INVALID");
        }
    }

    // Hàm tạo user mới từ Google token
    // Tự động tạo tài khoản khi user đăng nhập Google lần đầu
    private async Task<Models.Users> CreateGoogleUserAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        FirebaseToken decodedToken)
    {
        // Bước 1: Lấy thông tin từ Google token
        var email = decodedToken.Claims["email"].ToString()!;
        var name = decodedToken.Claims.ContainsKey("name") ? decodedToken.Claims["name"].ToString() : email;
        var picture = decodedToken.Claims.ContainsKey("picture") ? decodedToken.Claims["picture"].ToString() : null;

        // Bước 2: Lấy role mặc định "guard"
        var roles = await connection.GetAllAsync<Roles>(transaction);
        var defaultRole = roles.FirstOrDefault(r => r.Name == "guard" && !r.IsDeleted);
        if (defaultRole == null)
        {
            throw new NotFoundException("Default role 'guard' not found", "ROLE_NOT_FOUND");
        }

        // Bước 3: Tạo entity User mới
        var user = new Models.Users
        {
            Id = Guid.NewGuid(),
            FirebaseUid = decodedToken.Uid,
            Email = email!,
            FullName = name ?? email,
            AvatarUrl = picture,
            RoleId = defaultRole.Id,
            AuthProvider = "google",        // Đánh dấu là Google user
            Status = "active",
            IsActive = true,
            IsDeleted = false,
            EmailVerified = true,           // Google email đã verified
            EmailVerifiedAt = DateTime.UtcNow,
            LoginCount = 0,
            Password = string.Empty,        // Google user không có password
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Bước 4: INSERT user vào database
        await connection.InsertAsync(user, transaction);
        logger.LogInformation("Auto-created Google user: {Email}", user.Email);

        return user;
    }

    // Hàm verify user trên Firebase Authentication
    // Đảm bảo tài khoản Firebase không bị vô hiệu hóa
    private async Task VerifyFirebaseUserAsync(string firebaseUid)
    {
        try
        {
            var userRecord = await FirebaseAuth.DefaultInstance.GetUserAsync(firebaseUid);
            if (userRecord.Disabled)
            {
                throw new UnauthorizedException("Firebase account is disabled", "AUTH_FIREBASE_DISABLED");
            }
        }
        catch (FirebaseAuthException ex)
        {
            logger.LogError(ex, "Firebase user verification failed for UID: {FirebaseUid}", firebaseUid);
            throw new UnauthorizedException("Firebase authentication failed", ex, "AUTH_FIREBASE_FAILED");
        }
    }

    // Hàm tạo JWT Access Token
    // Chứa userId, email, roleId để authorization
    private string GenerateAccessToken(Models.Users user)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

            if (key.Length == 0)
            {
                throw new InvalidOperationException("JWT SecretKey cannot be empty");
            }

            // Tạo claims - thông tin được mã hóa trong token
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),      // Subject - userId
                new(JwtRegisteredClaimNames.Email, user.Email),            // Email
                new(ClaimTypes.Role, user.RoleId.ToString()),              // Role cho authorization
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),        // NameIdentifier - userId
                new("userId", user.Id.ToString()),                         // Custom claim
                new("roleId", user.RoleId.ToString()),                     // Custom claim - dùng cho RoleAuthorizationFilter
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // JWT ID - unique identifier
            };

            // Cấu hình token
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)  // HMAC SHA256 để ký token
            };

            // Tạo và serialize token
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);
            
            logger.LogDebug("Access token generated successfully for user {UserId}", user.Id);
            return tokenString;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating access token for user {UserId}", user.Id);
            throw;
        }
    }

    // Hàm tạo Refresh Token
    // Random 64 bytes string để bảo mật cao
    private string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    // Hàm lưu Access Token vào database
    // XÓA tất cả access tokens cũ, chỉ giữ token mới nhất
    private async Task SaveUserTokenAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        // Bước 1: XÓA tất cả access tokens cũ của user
        // Đảm bảo mỗi user chỉ có 1 access token active
        var existingTokens = await connection.GetAllAsync<UserTokens>(transaction);
        var userTokens = existingTokens.Where(t => 
            t.UserId == userId && 
            t.TokenType == "access_token").ToList();

        foreach (var existingToken in userTokens)
        {
            await connection.DeleteAsync(existingToken, transaction);
        }

        // Bước 2: INSERT access token mới
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

    // Hàm lưu Refresh Token vào database
    // XÓA tất cả refresh tokens cũ, chỉ giữ token mới nhất
    private async Task SaveRefreshTokenAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        // Bước 1: XÓA tất cả refresh tokens cũ của user
        var existingTokens = await connection.GetAllAsync<RefreshTokens>(transaction);
        var userRefreshTokens = existingTokens.Where(t => 
            t.UserId == userId).ToList();

        foreach (var existingToken in userRefreshTokens)
        {
            await connection.DeleteAsync(existingToken, transaction);
        }

        // Bước 2: INSERT refresh token mới
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

    // Hàm lưu hoặc cập nhật User Session
    // Reactivate session cũ nếu tồn tại, tạo mới nếu chưa có
    private async Task SaveUserSessionAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        DateTime expiresAt)
    {
        var sessions = await connection.GetAllAsync<UserSessions>(transaction);
        var existingSession = sessions.FirstOrDefault(s => s.UserId == userId && !s.IsDeleted);

        if (existingSession != null)
        {
            // Reactivate session cũ khi user login lại
            existingSession.IsActive = true;
            existingSession.ExpiresAt = expiresAt;
            existingSession.LastActivityAt = DateTime.UtcNow;
            existingSession.UpdatedAt = DateTime.UtcNow;
            await connection.UpdateAsync(existingSession, transaction);
        }
        else
        {
            // Tạo session mới
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

    // Hàm cập nhật thông tin đăng nhập cuối
    // Update LastLoginAt và tăng LoginCount
    private async Task UpdateLastLoginAsync(IDbConnection connection, IDbTransaction transaction, Guid userId)
    {
        var user = await connection.GetAsync<Models.Users>(userId, transaction);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.LoginCount++;              // Tăng số lần đăng nhập
            user.UpdatedAt = DateTime.UtcNow;
            await connection.UpdateAsync(user, transaction);
        }
    }

    // Hàm ghi log audit trail
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
