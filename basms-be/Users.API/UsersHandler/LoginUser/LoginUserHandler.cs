using BuildingBlocks.Exceptions;

namespace Users.API.UsersHandler.LoginUser;

public record LoginUserCommand(
    string Email,                  
    string? Password = null,      
    string? GoogleIdToken = null   
) : ICommand<LoginUserResult>;


public record LoginUserResult(
    Guid UserId,                    
    string Email,                 
    string FullName,               
    Guid RoleId,                   
    string AccessToken,             
    string RefreshToken,            
    DateTime AccessTokenExpiry,    
    DateTime RefreshTokenExpiry,   
    DateTime SessionExpiry         
);

public class LoginUserHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<LoginUserHandler> logger,
    LoginUserValidator validator,       
    IOptions<JwtSettings> jwtSettings)       
    : ICommandHandler<LoginUserCommand, LoginUserResult>
{
    private readonly JwtSettings _jwtSettings = jwtSettings.Value;

    public async Task<LoginUserResult> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        ValidateJwtSettings();
        
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
            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                Models.Users user;
                if (!string.IsNullOrEmpty(command.GoogleIdToken))
                {
                    user = await AuthenticateWithGoogleAsync(connection, transaction, command.GoogleIdToken);
                }
                else
                {
                    user = await AuthenticateWithEmailPasswordAsync(connection, transaction, command.Email, command.Password!);
                }
                
                await VerifyFirebaseUserAsync(user.FirebaseUid);
                
                var accessToken = GenerateAccessToken(user);
                var refreshToken = GenerateRefreshToken();
                
                var accessTokenExpiry = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);
                var refreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
                var sessionExpiry = DateTime.UtcNow.AddDays(30);
                
                await SaveUserTokenAsync(connection, transaction, user.Id, accessToken, accessTokenExpiry);


                await SaveRefreshTokenAsync(connection, transaction, user.Id, refreshToken, refreshTokenExpiry);


                await SaveUserSessionAsync(connection, transaction, user.Id, sessionExpiry);
                
                
                await UpdateLastLoginAsync(connection, transaction, user.Id);
                
                await LogAuditAsync(connection, transaction, user.Id, "LOGIN");
                
                transaction.Commit();

                logger.LogInformation("User logged in successfully: {Email}, UserId: {UserId}", user.Email, user.Id);


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


    private async Task<Models.Users> AuthenticateWithEmailPasswordAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string email,
        string password)
    {
        var users = await connection.GetAllAsync<Models.Users>(transaction);
        var user = users.FirstOrDefault(u => u.Email == email && !u.IsDeleted);

        if (user == null)
        {
            logger.LogWarning("Login failed: User not found with email {Email}", email);
            throw new UnauthorizedException("Invalid email or password", "AUTH_INVALID_CREDENTIALS");
        }
        
        if (!user.IsActive)
        {
            logger.LogWarning("Login failed: User account inactive for email {Email}", email);
            throw new UnauthorizedException("Account is inactive. Please contact support.", "AUTH_ACCOUNT_INACTIVE");
        }
        
        if (string.IsNullOrEmpty(user.Password))
        {
            logger.LogWarning("Login failed: No password set for user {Email}. User may have registered with Google.", email);
            throw new UnauthorizedException("This account was registered with Google. Please use Google sign-in.", "AUTH_USE_GOOGLE_LOGIN");
        }
        
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


    private async Task<Models.Users> AuthenticateWithGoogleAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string googleIdToken)
    {
        try
        {
            var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(googleIdToken);
            var firebaseUid = decodedToken.Uid;
            var email = decodedToken.Claims.ContainsKey("email") ? decodedToken.Claims["email"].ToString() : null;

            if (string.IsNullOrEmpty(email))
            {
                throw new UnauthorizedException("Email not found in Google token", "AUTH_GOOGLE_EMAIL_MISSING");
            }


            var users = await connection.GetAllAsync<Models.Users>(transaction);
            var user = users.FirstOrDefault(u => 
                (u.FirebaseUid == firebaseUid || u.Email == email) && !u.IsDeleted);

            if (user == null)
            {
                user = await CreateGoogleUserAsync(connection, transaction, decodedToken);
            }
            else if (user.FirebaseUid != firebaseUid)
            {
                user.FirebaseUid = firebaseUid;
                user.AuthProvider = "google";
                user.UpdatedAt = DateTime.UtcNow;
                await connection.UpdateAsync(user, transaction);
            }
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
    
    private async Task<Models.Users> CreateGoogleUserAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        FirebaseToken decodedToken)
    {
        var email = decodedToken.Claims["email"].ToString()!;
        var name = decodedToken.Claims.ContainsKey("name") ? decodedToken.Claims["name"].ToString() : email;
        var picture = decodedToken.Claims.ContainsKey("picture") ? decodedToken.Claims["picture"].ToString() : null;
        var roles = await connection.GetAllAsync<Roles>(transaction);
        var defaultRole = roles.FirstOrDefault(r => r.Name == "guard" && !r.IsDeleted);
        if (defaultRole == null)
        {
            throw new NotFoundException("Default role 'guard' not found", "ROLE_NOT_FOUND");
        }

        var user = new Models.Users
        {
            Id = Guid.NewGuid(),
            FirebaseUid = decodedToken.Uid,
            Email = email!,
            FullName = name ?? email,
            AvatarUrl = picture,
            RoleId = defaultRole.Id,
            AuthProvider = "google",        
            Status = "active",
            IsActive = true,
            IsDeleted = false,
            EmailVerified = true,        
            EmailVerifiedAt = DateTime.UtcNow,
            LoginCount = 0,
            Password = string.Empty,       
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        await connection.InsertAsync(user, transaction);
        logger.LogInformation("Auto-created Google user: {Email}", user.Email);

        return user;
    }


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
        var existingTokens = await connection.GetAllAsync<UserTokens>(transaction);
        var userTokens = existingTokens.Where(t => 
            t.UserId == userId && 
            t.TokenType == "access_token").ToList();

        foreach (var existingToken in userTokens)
        {
            await connection.DeleteAsync(existingToken, transaction);
        }
        
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


    private async Task SaveRefreshTokenAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        var existingTokens = await connection.GetAllAsync<RefreshTokens>(transaction);
        var userRefreshTokens = existingTokens.Where(t => 
            t.UserId == userId).ToList();

        foreach (var existingToken in userRefreshTokens)
        {
            await connection.DeleteAsync(existingToken, transaction);
        }
        
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
            existingSession.IsActive = true;
            existingSession.ExpiresAt = expiresAt;
            existingSession.LastActivityAt = DateTime.UtcNow;
            existingSession.UpdatedAt = DateTime.UtcNow;
            await connection.UpdateAsync(existingSession, transaction);
        }
        else
        {
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
    
    private async Task UpdateLastLoginAsync(IDbConnection connection, IDbTransaction transaction, Guid userId)
    {
        var user = await connection.GetAsync<Models.Users>(userId, transaction);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.LoginCount++;              
            user.UpdatedAt = DateTime.UtcNow;
            await connection.UpdateAsync(user, transaction);
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
