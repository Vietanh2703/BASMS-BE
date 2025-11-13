using CreateUserRequest = BuildingBlocks.Messaging.Events.CreateUserRequest;
using CreateUserResponse = BuildingBlocks.Messaging.Events.CreateUserResponse;

namespace Users.API.Consumers;

/// <summary>
/// Consumer xử lý request tạo user từ các service khác (Contracts.API)
/// Receives CreateUserRequest từ BuildingBlocks.Messaging.Contracts
/// </summary>
public class CreateUserRequestConsumer(
    IDbConnectionFactory connectionFactory,
    ILogger<CreateUserRequestConsumer> logger,
    Users.API.Extensions.EmailHandler emailHandler)
    : IConsumer<CreateUserRequest>
{
    public async Task Consume(ConsumeContext<CreateUserRequest> context)
    {
        var request = context.Message;

        logger.LogInformation(
            "Received CreateUserRequest for email: {Email} with role: {Role}",
            request.Email, request.RoleName);

        try
        {
            // ================================================================
            // BƯỚC 1: VALIDATE EMAIL CHƯA TỒN TẠI
            // ================================================================
            using var connection = await connectionFactory.CreateConnectionAsync();

            var existingUsers = await connection.GetAllAsync<Models.Users>();
            var existingUser = existingUsers.FirstOrDefault(u =>
                u.Email == request.Email && !u.IsDeleted);

            if (existingUser != null)
            {
                logger.LogWarning("User with email {Email} already exists", request.Email);

                await context.RespondAsync(new CreateUserResponse
                {
                    Success = false,
                    ErrorMessage = $"Email {request.Email} already exists",
                    Email = request.Email,
                    FullName = request.FullName
                });
                return;
            }

            // ================================================================
            // BƯỚC 2: LẤY ROLE ID
            // ================================================================
            var roles = await connection.GetAllAsync<Roles>();
            var role = roles.FirstOrDefault(r =>
                r.Name.Equals(request.RoleName, StringComparison.OrdinalIgnoreCase) && !r.IsDeleted);

            if (role == null)
            {
                logger.LogError("Role {RoleName} not found", request.RoleName);

                await context.RespondAsync(new CreateUserResponse
                {
                    Success = false,
                    ErrorMessage = $"Role '{request.RoleName}' not found",
                    Email = request.Email,
                    FullName = request.FullName
                });
                return;
            }

            // ================================================================
            // BƯỚC 3: TẠO FIREBASE USER
            // ================================================================
            UserRecord? firebaseUser = null;
            try
            {
                var firebaseAuth = FirebaseAuth.DefaultInstance;
                var userRecordArgs = new UserRecordArgs
                {
                    Email = request.Email,
                    Password = request.Password,
                    DisplayName = request.FullName,
                    PhotoUrl = request.AvatarUrl,
                    EmailVerified = false,
                    Disabled = false
                };

                if (!string.IsNullOrEmpty(request.Phone))
                {
                    userRecordArgs.PhoneNumber = request.Phone;
                }

                firebaseUser = await firebaseAuth.CreateUserAsync(userRecordArgs);
                logger.LogInformation("Firebase user created: {FirebaseUid}", firebaseUser.Uid);
            }
            catch (FirebaseAuthException ex)
            {
                logger.LogError(ex, "Firebase error creating user: {Email}", request.Email);

                await context.RespondAsync(new CreateUserResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to create Firebase user: {ex.Message}",
                    Email = request.Email,
                    FullName = request.FullName
                });
                return;
            }

            // ================================================================
            // BƯỚC 4: LƯU VÀO DATABASE
            // ================================================================
            using var transaction = connection.BeginTransaction();

            try
            {
                var user = new Models.Users
                {
                    Id = Guid.NewGuid(),
                    IdentityNumber = request.IdentityNumber,
                    FirebaseUid = firebaseUser.Uid,
                    Email = request.Email,
                    FullName = request.FullName,
                    Phone = request.Phone,
                    Gender = request.Gender,
                    Address = request.Address,
                    RoleId = role.Id,
                    AvatarUrl = request.AvatarUrl,
                    AuthProvider = request.AuthProvider,
                    Status = "active",
                    IsActive = true,
                    IsDeleted = false,
                    LoginCount = 0,
                    Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await connection.InsertAsync(user, transaction);
                logger.LogInformation("User inserted into database: {UserId}", user.Id);

                // Log audit
                var auditLog = new AuditLogs
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Action = "CREATE_USER_VIA_REQUEST",
                    EntityType = "User",
                    EntityId = user.Id,
                    NewValues = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        user.Email,
                        user.FullName,
                        user.RoleId,
                        RoleName = request.RoleName,
                        Source = "Contracts.API"
                    }),
                    Status = "success",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await connection.InsertAsync(auditLog, transaction);
                transaction.Commit();

                logger.LogInformation(
                    "User created successfully via request: {Email} (Role: {RoleName})",
                    user.Email, request.RoleName);

                // ================================================================
                // BƯỚC 5: TRẢ VỀ RESPONSE THÀNH CÔNG
                // ================================================================
                await context.RespondAsync(new CreateUserResponse
                {
                    Success = true,
                    UserId = user.Id,
                    FirebaseUid = user.FirebaseUid,
                    Email = user.Email,
                    FullName = user.FullName,
                    GeneratedPassword = request.Password
                });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                logger.LogError(ex, "Error saving user to database: {Email}", request.Email);

                // Cleanup Firebase user if DB fails
                if (firebaseUser != null)
                {
                    try
                    {
                        await FirebaseAuth.DefaultInstance.DeleteUserAsync(firebaseUser.Uid);
                        logger.LogInformation("Cleaned up Firebase user after DB failure: {FirebaseUid}", firebaseUser.Uid);
                    }
                    catch (Exception cleanupEx)
                    {
                        logger.LogError(cleanupEx, "Failed to cleanup Firebase user: {FirebaseUid}", firebaseUser.Uid);
                    }
                }

                await context.RespondAsync(new CreateUserResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to save user to database: {ex.Message}",
                    Email = request.Email,
                    FullName = request.FullName
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error creating user via request: {Email}", request.Email);

            await context.RespondAsync(new CreateUserResponse
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                Email = request.Email,
                FullName = request.FullName
            });
        }
    }
}
