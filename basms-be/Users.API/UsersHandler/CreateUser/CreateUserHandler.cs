namespace Users.API.UsersHandler.CreateUser;

public record CreateUserCommand(
    string IdentityNumber,
    DateTime IdentityIssueDate,
    string IdentityIssuePlace,
    string Email,
    string Password,
    string FullName,
    string? Phone = null,
    string? Gender = null,
    string? Address = null,
    DateOnly? DateOfBirth = null,  
    int? BirthDay = null,
    int? BirthMonth = null,
    int? BirthYear = null,
    Guid? RoleId = null,
    string? AvatarUrl = null,
    string AuthProvider = "email"
) : ICommand<CreateUserResult>;

public record CreateUserResult(Guid Id, string FirebaseUid, string Email);

public class CreateUserHandler(
    IDbConnectionFactory connectionFactory,  
    ILogger<CreateUserHandler> logger,       
    CreateUserValidator validator,          
    EmailHandler emailHandler,             
    Messaging.UserEventPublisher eventPublisher) 
    : ICommandHandler<CreateUserCommand, CreateUserResult>
{
    public async Task<CreateUserResult> Handle(CreateUserCommand command, CancellationToken cancellationToken)
{
    var validationResult = await validator.ValidateAsync(command, cancellationToken);
    if (!validationResult.IsValid)
    {
        var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
        throw new InvalidOperationException($"Validation failed: {errors}");
    }
    await connectionFactory.EnsureTablesCreatedAsync();
    UserRecord firebaseUser = null;
    try
    {
        firebaseUser = await CreateFirebaseUserAsync(command);
        logger.LogDebug("Firebase user created: {FirebaseUid}", firebaseUser.Uid);
    }
    catch (Exception firebaseEx)
    {
        logger.LogError(firebaseEx, "Failed to create Firebase user for {Email}", command.Email);
        throw new InvalidOperationException($"Failed to create Firebase user: {firebaseEx.Message}", firebaseEx);
    }

    try
    {
        using var connection = await connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            var existingUsers = await connection.GetAllAsync<Models.Users>(transaction);
            var existingUser = existingUsers.FirstOrDefault(u =>
                u.Email == command.Email && !u.IsDeleted);

            if (existingUser != null)
            {
                throw new InvalidOperationException($"Email {command.Email} already exists");
            }
            Guid roleId = command.RoleId ?? await GetDefaultRoleIdAsync(connection, transaction);

            // ✅ FIX: Lấy role object NGAY sau khi có roleId, trước khi commit transaction
            var role = await connection.GetAsync<Roles>(roleId, transaction);
            if (role == null)
            {
                throw new InvalidOperationException($"Role with ID {roleId} not found");
            }

            int? birthDay = command.BirthDay;
            int? birthMonth = command.BirthMonth;
            int? birthYear = command.BirthYear;

            if (command.DateOfBirth.HasValue && (!birthDay.HasValue || !birthMonth.HasValue || !birthYear.HasValue))
            {
                birthDay = command.DateOfBirth.Value.Day;
                birthMonth = command.DateOfBirth.Value.Month;
                birthYear = command.DateOfBirth.Value.Year;
            }
            var customerRoleId = Guid.Parse("ddbd630a-ba6e-11f0-bcac-00155dca8f48");
            var isActive = roleId != customerRoleId;

            logger.LogInformation(
                "Creating user with RoleId: {RoleId} ({RoleName}), IsActive: {IsActive}",
                roleId, role.Name, isActive);
            var user = new Models.Users
            {
                Id = Guid.NewGuid(),
                FirebaseUid = firebaseUser.Uid,
                IdentityNumber = command.IdentityNumber,
                IdentityIssueDate = command.IdentityIssueDate,
                IdentityIssuePlace = command.IdentityIssuePlace,
                Email = command.Email,
                FullName = command.FullName,
                Phone = command.Phone,
                Gender = command.Gender,
                Address = command.Address,
                BirthDay = birthDay,
                BirthMonth = birthMonth,
                BirthYear = birthYear,
                RoleId = roleId,
                AvatarUrl = command.AvatarUrl,
                AuthProvider = command.AuthProvider,
                Status = "active",
                IsActive = isActive,
                IsDeleted = false,
                LoginCount = 0,
                Password = BCrypt.Net.BCrypt.HashPassword(command.Password),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            await connection.InsertAsync(user, transaction);
            logger.LogDebug("User inserted into database: {UserId}", user.Id);
            
            await LogAuditAsync(connection, transaction, user);
            logger.LogDebug("Audit log created for user: {UserId}", user.Id);
            
            transaction.Commit();
            logger.LogDebug("Transaction committed successfully for user: {UserId}", user.Id);

            // ✅ FIX: Sử dụng role object đã lấy từ trước (line 71), không cần fetch lại
            // Publish UserCreatedEvent để các service khác (Contracts.API, Shifts.API) nhận được
            try
            {
                await eventPublisher.PublishUserCreatedAsync(user, role, cancellationToken);
                logger.LogInformation(
                    "UserCreatedEvent published successfully for user: {UserId} with role: {RoleName}",
                    user.Id,
                    role.Name);
            }
            catch (Exception eventEx)
            {
                logger.LogError(eventEx, "Failed to publish UserCreatedEvent for user {UserId}, but user was created successfully", user.Id);
            }
            var customerRoleIdForEmail = Guid.Parse("ddbd630a-ba6e-11f0-bcac-00155dca8f48");
            if (roleId != customerRoleIdForEmail)
            {
                try
                {
                    await emailHandler.SendWelcomeEmailAsync(user.FullName, user.Email, command.Password);
                    logger.LogInformation("Welcome email sent successfully to {RoleName}: {Email}", role.Name, user.Email);
                }
                catch (Exception emailEx)
                {
                    logger.LogError(emailEx, "Failed to send welcome email to {Email}, but user was created successfully", user.Email);
                }
            }
            else
            {
                logger.LogInformation("Skipping welcome email for customer: {Email} (customer needs admin activation)", user.Email);
            }

            logger.LogInformation("User created successfully: {Email}, FirebaseUid: {FirebaseUid}",
                user.Email, user.FirebaseUid);

            return new CreateUserResult(user.Id, user.FirebaseUid, user.Email);
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
        if (firebaseUser != null)
        {
            try
            {
                await FirebaseAuth.DefaultInstance.DeleteUserAsync(firebaseUser.Uid);
                logger.LogWarning("Deleted Firebase user {FirebaseUid} due to database error", firebaseUser.Uid);
            }
            catch (Exception deleteEx)
            {
                logger.LogError(deleteEx, "Failed to delete Firebase user {FirebaseUid} after database error", firebaseUser.Uid);
            }
        }

        logger.LogError(ex, "Error creating user: {Email}", command.Email);
        throw;
    }
}
    
    private async Task<UserRecord> CreateFirebaseUserAsync(CreateUserCommand command)
    {
        try
        {
            var firebaseAuth = FirebaseAuth.DefaultInstance;
            if (firebaseAuth == null)
            {
                logger.LogError("FirebaseAuth.DefaultInstance is null. Firebase may not be initialized properly.");
                throw new InvalidOperationException("Firebase Authentication is not initialized. Please check Firebase configuration.");
            }
            var userRecordArgs = new UserRecordArgs
            {
                Email = command.Email,
                Password = command.Password,
                DisplayName = command.FullName,
                PhotoUrl = command.AvatarUrl,
                EmailVerified = false, 
                Disabled = false        
            };
            if (!string.IsNullOrEmpty(command.Phone))
            {
                userRecordArgs.PhoneNumber = command.Phone;
            }
            return await firebaseAuth.CreateUserAsync(userRecordArgs);
        }
        catch (ArgumentNullException ex)
        {
            logger.LogError(ex, "Null argument when creating Firebase user");
            throw new InvalidOperationException("Invalid user data provided for Firebase authentication.", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error creating Firebase user");
            throw;
        }
    }
    private async Task<Guid> GetDefaultRoleIdAsync(IDbConnection connection, IDbTransaction transaction)
    {
        var roles = await connection.GetAllAsync<Roles>(transaction);
        var defaultRole = roles.FirstOrDefault(r => r.Name == "guard" && !r.IsDeleted);

        if (defaultRole == null)
        {
            throw new InvalidOperationException("Default role 'guard' not found");
        }

        return defaultRole.Id;
    }
    private async Task LogAuditAsync(IDbConnection connection, IDbTransaction transaction, Models.Users user)
    {
        var auditLog = new AuditLogs
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "CREATE_USER",          
            EntityType = "User",             
            EntityId = user.Id,              
            NewValues = JsonSerializer.Serialize(new 
            {
                user.IdentityNumber,
                user.Email,
                user.FullName,
                user.RoleId,
                user.Status,
                user.FirebaseUid
            }),
            IpAddress = null,               
            Status = "success",              
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await connection.InsertAsync(auditLog, transaction);
    }
}