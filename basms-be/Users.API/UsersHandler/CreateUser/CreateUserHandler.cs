using Users.API.Extensions;

namespace Users.API.UsersHandler.CreateUser;

public record CreateUserCommand(
    string Email,
    string Password,
    string FullName,
    string? Phone = null,
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
    EmailHandler emailHandler)
    : ICommandHandler<CreateUserCommand, CreateUserResult>
{
    public async Task<CreateUserResult> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Validation failed: {errors}");
        }

        // Ensure tables are created before first user creation
        await connectionFactory.EnsureTablesCreatedAsync();

        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Step 1: Validate email uniqueness
                var existingUsers = await connection.GetAllAsync<Models.Users>(transaction);
                var existingUser = existingUsers.FirstOrDefault(u => 
                    u.Email == command.Email && !u.IsDeleted);

                if (existingUser != null)
                {
                    throw new InvalidOperationException($"Email {command.Email} already exists");
                }

                // Step 2: Get or create default role if not provided
                Guid roleId = command.RoleId ?? await GetDefaultRoleIdAsync(connection, transaction);

                // Step 3: Create user in Firebase Authentication
                var firebaseUser = await CreateFirebaseUserAsync(command);

                // Step 4: Auto-calculate birth date fields if DateOfBirth is provided
                int? birthDay = command.BirthDay;
                int? birthMonth = command.BirthMonth;
                int? birthYear = command.BirthYear;

                if (command.DateOfBirth.HasValue && (!birthDay.HasValue || !birthMonth.HasValue || !birthYear.HasValue))
                {
                    birthDay = command.DateOfBirth.Value.Day;
                    birthMonth = command.DateOfBirth.Value.Month;
                    birthYear = command.DateOfBirth.Value.Year;
                }

                // Step 5: Create user entity
                var user = new Models.Users
                {
                    Id = Guid.NewGuid(),
                    FirebaseUid = firebaseUser.Uid,
                    Email = command.Email,
                    FullName = command.FullName,
                    Phone = command.Phone,
                    Address = command.Address,
                    BirthDay = birthDay,
                    BirthMonth = birthMonth,
                    BirthYear = birthYear,
                    RoleId = roleId,
                    AvatarUrl = command.AvatarUrl,
                    AuthProvider = command.AuthProvider,
                    Status = "active",
                    IsActive = true,
                    IsDeleted = false,
                    LoginCount = 0,
                    Password = BCrypt.Net.BCrypt.HashPassword(command.Password),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Step 6: Save user to database (INSERT INTO users table)
                await connection.InsertAsync(user, transaction);
                logger.LogDebug("User inserted into database: {UserId}", user.Id);

                // Step 7: Log audit trail to database (INSERT INTO audit_logs table)
                await LogAuditAsync(connection, transaction, user);
                logger.LogDebug("Audit log created for user: {UserId}", user.Id);

                // Step 8: Commit transaction to save all changes to database
                transaction.Commit();
                logger.LogDebug("Transaction committed successfully for user: {UserId}", user.Id);

                // Step 9: Send welcome email to user
                try
                {
                    await emailHandler.SendWelcomeEmailAsync(user.FullName, user.Email, command.Password);
                    logger.LogInformation("Welcome email sent successfully to {Email}", user.Email);
                }
                catch (Exception emailEx)
                {
                    // Log error but don't fail the user creation
                    logger.LogError(emailEx, "Failed to send welcome email to {Email}, but user was created successfully", user.Email);
                }

                logger.LogInformation("User created successfully: {Email}, FirebaseUid: {FirebaseUid}",
                    user.Email, user.FirebaseUid);

                // Step 10: Return result with created user information
                var result = new CreateUserResult(user.Id, user.FirebaseUid, user.Email);
                return result;
            }
            catch
            {
                // Rollback transaction if any error occurs
                transaction.Rollback();
                logger.LogWarning("Transaction rolled back due to error");
                throw;
            }
        }
        catch (FirebaseAuthException ex)
        {
            logger.LogError(ex, "Firebase error creating user: {Email}", command.Email);
            throw new InvalidOperationException($"Failed to create Firebase user: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating user: {Email}", command.Email);
            throw;
        }
    }

    private async Task<UserRecord> CreateFirebaseUserAsync(CreateUserCommand command)
    {
        try
        {
            // Check if FirebaseAuth is initialized
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
            NewValues = System.Text.Json.JsonSerializer.Serialize(new
            {
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