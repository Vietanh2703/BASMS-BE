// Handler xử lý logic tạo user mới
// Thực hiện: Validate -> Tạo Firebase account -> Lưu database -> Gửi email -> Log audit

namespace Users.API.UsersHandler.CreateUser;

// Command chứa dữ liệu để tạo user - được gửi từ Endpoint đến Handler
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

// Result trả về từ Handler cho Endpoint
public record CreateUserResult(Guid Id, string FirebaseUid, string Email);

public class CreateUserHandler(
    IDbConnectionFactory connectionFactory,  // Factory để tạo kết nối database
    ILogger<CreateUserHandler> logger,       // Logger để ghi log
    CreateUserValidator validator,          // Validator để kiểm tra dữ liệu đầu vào
    EmailHandler emailHandler,              // Handler để gửi email
    Users.API.Messaging.UserEventPublisher eventPublisher)  // Publisher để gửi events đến RabbitMQ
    : ICommandHandler<CreateUserCommand, CreateUserResult>
{
    public async Task<CreateUserResult> Handle(CreateUserCommand command, CancellationToken cancellationToken)
{
    // Bước 1: Validate dữ liệu đầu vào bằng FluentValidation
    var validationResult = await validator.ValidateAsync(command, cancellationToken);
    if (!validationResult.IsValid)
    {
        var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
        throw new InvalidOperationException($"Validation failed: {errors}");
    }

    // Bước 2: Đảm bảo tất cả bảng database đã được tạo
    await connectionFactory.EnsureTablesCreatedAsync();

    // Bước 3: TẠO FIREBASE USER TRƯỚC
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
        // Bước 4: Tạo kết nối database và bắt đầu transaction
        using var connection = await connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Bước 5: Kiểm tra email đã tồn tại chưa
            var existingUsers = await connection.GetAllAsync<Models.Users>(transaction);
            var existingUser = existingUsers.FirstOrDefault(u =>
                u.Email == command.Email && !u.IsDeleted);

            if (existingUser != null)
            {
                throw new InvalidOperationException($"Email {command.Email} already exists");
            }

            // Bước 6: Lấy hoặc tạo role mặc định
            Guid roleId = command.RoleId ?? await GetDefaultRoleIdAsync(connection, transaction);

            // Bước 7: Tự động tính toán các trường ngày sinh
            int? birthDay = command.BirthDay;
            int? birthMonth = command.BirthMonth;
            int? birthYear = command.BirthYear;

            if (command.DateOfBirth.HasValue && (!birthDay.HasValue || !birthMonth.HasValue || !birthYear.HasValue))
            {
                birthDay = command.DateOfBirth.Value.Day;
                birthMonth = command.DateOfBirth.Value.Month;
                birthYear = command.DateOfBirth.Value.Year;
            }

            // Bước 8: Tạo entity User với FirebaseUid đã có
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
                IsActive = true,
                IsDeleted = false,
                LoginCount = 0,
                Password = BCrypt.Net.BCrypt.HashPassword(command.Password),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Bước 9: INSERT user vào database
            await connection.InsertAsync(user, transaction);
            logger.LogDebug("User inserted into database: {UserId}", user.Id);

            // Bước 10: Ghi log audit trail
            await LogAuditAsync(connection, transaction, user);
            logger.LogDebug("Audit log created for user: {UserId}", user.Id);

            // Bước 11: Commit transaction
            transaction.Commit();
            logger.LogDebug("Transaction committed successfully for user: {UserId}", user.Id);

            // Bước 12: Lấy thông tin role để publish event
            var role = await connection.GetAsync<Roles>(roleId);
            if (role == null)
            {
                logger.LogWarning("Role not found with ID {RoleId} for user {UserId}", roleId, user.Id);
            }

            // Bước 13: Publish UserCreatedEvent to RabbitMQ
            if (role != null)
            {
                try
                {
                    await eventPublisher.PublishUserCreatedAsync(user, role, cancellationToken);
                    logger.LogInformation("UserCreatedEvent published successfully for user: {UserId}", user.Id);
                }
                catch (Exception eventEx)
                {
                    logger.LogError(eventEx, "Failed to publish UserCreatedEvent for user {UserId}, but user was created successfully", user.Id);
                }
            }

            // Bước 14: Gửi email chào mừng
            try
            {
                await emailHandler.SendWelcomeEmailAsync(user.FullName, user.Email, command.Password);
                logger.LogInformation("Welcome email sent successfully to {Email}", user.Email);
            }
            catch (Exception emailEx)
            {
                logger.LogError(emailEx, "Failed to send welcome email to {Email}, but user was created successfully", user.Email);
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
        // NẾU DATABASE LỖI, XÓA FIREBASE USER ĐÃ TẠO
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



    // Hàm tạo user trên Firebase Authentication
    private async Task<UserRecord> CreateFirebaseUserAsync(CreateUserCommand command)
    {
        try
        {
            // Kiểm tra FirebaseAuth đã được khởi tạo chưa
            var firebaseAuth = FirebaseAuth.DefaultInstance;
            if (firebaseAuth == null)
            {
                logger.LogError("FirebaseAuth.DefaultInstance is null. Firebase may not be initialized properly.");
                throw new InvalidOperationException("Firebase Authentication is not initialized. Please check Firebase configuration.");
            }

            // Chuẩn bị dữ liệu để tạo user trên Firebase
            var userRecordArgs = new UserRecordArgs
            {
                Email = command.Email,
                Password = command.Password,
                DisplayName = command.FullName,
                PhotoUrl = command.AvatarUrl,
                EmailVerified = false,  // Email chưa được xác thực
                Disabled = false        // Tài khoản không bị vô hiệu hóa
            };

            // Thêm số điện thoại nếu có
            if (!string.IsNullOrEmpty(command.Phone))
            {
                userRecordArgs.PhoneNumber = command.Phone;
            }

            // Gọi Firebase API để tạo user
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

    // Hàm lấy role mặc định "guard" từ database
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

    // Hàm ghi log audit trail vào database
    // Lưu lại hành động CREATE_USER để theo dõi và audit
    private async Task LogAuditAsync(IDbConnection connection, IDbTransaction transaction, Models.Users user)
    {
        var auditLog = new AuditLogs
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "CREATE_USER",          // Loại hành động
            EntityType = "User",             // Loại entity
            EntityId = user.Id,              // ID của entity
            NewValues = System.Text.Json.JsonSerializer.Serialize(new  // Giá trị mới (JSON)
            {
                user.IdentityNumber,
                user.Email,
                user.FullName,
                user.RoleId,
                user.Status,
                user.FirebaseUid
            }),
            IpAddress = null,                // IP address (có thể thêm sau)
            Status = "success",              // Trạng thái thành công
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // INSERT audit log vào database
        await connection.InsertAsync(auditLog, transaction);
    }
}