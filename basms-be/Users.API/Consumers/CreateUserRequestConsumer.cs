using CreateUserRequest = BuildingBlocks.Messaging.Events.CreateUserRequest;
using CreateUserResponse = BuildingBlocks.Messaging.Events.CreateUserResponse;
using Dapper;

namespace Users.API.Consumers;

public class CreateUserRequestConsumer(
    IDbConnectionFactory connectionFactory,
    ILogger<CreateUserRequestConsumer> logger,
    Messaging.UserEventPublisher eventPublisher)
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
            using var connection = await connectionFactory.CreateConnectionAsync();

            var existingUser = await connection.QueryFirstOrDefaultAsync<Models.Users>(
                "SELECT * FROM users WHERE Email = @Email AND IsDeleted = 0 LIMIT 1",
                new { Email = request.Email });

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

            var role = await connection.QueryFirstOrDefaultAsync<Roles>(
                "SELECT * FROM roles WHERE Name = @RoleName AND IsDeleted = 0 LIMIT 1",
                new { RoleName = request.RoleName });

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
            
            using var transaction = connection.BeginTransaction();

            try
            {

                var customerRoleId = Guid.Parse("ddbd630a-ba6e-11f0-bcac-00155dca8f48");
                var isActive = role.Id != customerRoleId; 

                logger.LogInformation(
                    "Creating user via request with RoleId: {RoleId} ({RoleName}), IsActive: {IsActive}",
                    role.Id, role.Name, isActive);

                var user = new Models.Users
                {
                    Id = Guid.NewGuid(),
                    IdentityNumber = request.IdentityNumber,
                    IdentityIssueDate = request.IdentityIssueDate ?? DateTime.UtcNow,
                    IdentityIssuePlace = request.IdentityIssuePlace ?? string.Empty,
                    FirebaseUid = firebaseUser.Uid,
                    Email = request.Email,
                    FullName = request.FullName,
                    Phone = request.Phone,
                    Gender = request.Gender,
                    Address = request.Address,
                    BirthDay = request.BirthDay,
                    BirthMonth = request.BirthMonth,
                    BirthYear = request.BirthYear,
                    RoleId = role.Id,
                    AvatarUrl = request.AvatarUrl,
                    AuthProvider = request.AuthProvider,
                    Status = "active",
                    IsActive = isActive,
                    IsDeleted = false,
                    LoginCount = 0,
                    Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await connection.InsertAsync(user, transaction);
                logger.LogInformation("User inserted into database: {UserId}", user.Id);
                
                var auditLog = new AuditLogs
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Action = "CREATE_USER_VIA_REQUEST",
                    EntityType = "User",
                    EntityId = user.Id,
                    NewValues = JsonSerializer.Serialize(new
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
                
                try
                {
                    await eventPublisher.PublishUserCreatedAsync(user, role, context.CancellationToken);
                    logger.LogInformation(
                        "✓ Published UserCreatedEvent for User {UserId} with Role {RoleName}",
                        user.Id, role.Name);
                }
                catch (Exception publishEx)
                {
                    logger.LogError(publishEx,
                        "Failed to publish UserCreatedEvent for User {UserId}. " +
                        "User was created but downstream services may not be notified.",
                        user.Id);
                }
                
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
