// Handler xử lý logic tạo user mới
// Thực hiện: Validate -> Tạo Firebase account -> Lưu database -> Gửi email -> Log audit

namespace Users.API.UsersHandler.CreateUser;

// Command chứa dữ liệu để tạo user - được gửi từ Endpoint đến Handler
public record CreateUserCommand(
    string Email,
    string Password,
    string FullName,
    string? Phone = null,
    string? Address = null,
    DateOnly? DateOfBirth = null,  // DateOnly để tính BirthDay/Month/Year
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
        // Kiểm tra email format, password strength, required fields, etc.
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Validation failed: {errors}");
        }

        // Bước 2: Đảm bảo tất cả bảng database đã được tạo (chạy migration nếu cần)
        await connectionFactory.EnsureTablesCreatedAsync();

        try
        {
            // Bước 3: Tạo kết nối database và bắt đầu transaction
            // Transaction đảm bảo tất cả thay đổi DB cùng commit hoặc rollback
            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Bước 4: Kiểm tra email đã tồn tại chưa (unique constraint)
                var existingUsers = await connection.GetAllAsync<Models.Users>(transaction);
                var existingUser = existingUsers.FirstOrDefault(u => 
                    u.Email == command.Email && !u.IsDeleted);

                if (existingUser != null)
                {
                    throw new InvalidOperationException($"Email {command.Email} already exists");
                }

                // Bước 5: Lấy hoặc tạo role mặc định nếu không được cung cấp
                // Mặc định là role "guard"
                Guid roleId = command.RoleId ?? await GetDefaultRoleIdAsync(connection, transaction);

                // Bước 6: Tạo tài khoản trên Firebase Authentication
                // Firebase sẽ quản lý authentication và trả về UID
                var firebaseUser = await CreateFirebaseUserAsync(command);

                // Bước 7: Tự động tính toán các trường ngày sinh
                // Nếu có DateOfBirth thì split ra BirthDay, BirthMonth, BirthYear
                int? birthDay = command.BirthDay;
                int? birthMonth = command.BirthMonth;
                int? birthYear = command.BirthYear;

                if (command.DateOfBirth.HasValue && (!birthDay.HasValue || !birthMonth.HasValue || !birthYear.HasValue))
                {
                    birthDay = command.DateOfBirth.Value.Day;
                    birthMonth = command.DateOfBirth.Value.Month;
                    birthYear = command.DateOfBirth.Value.Year;
                }

                // Bước 8: Tạo entity User để lưu vào database
                var user = new Models.Users
                {
                    Id = Guid.NewGuid(),                    // Tạo ID mới
                    FirebaseUid = firebaseUser.Uid,         // UID từ Firebase
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
                    Status = "active",                      // Trạng thái mặc định
                    IsActive = true,                        // Kích hoạt ngay
                    IsDeleted = false,                      // Chưa bị xóa
                    LoginCount = 0,                         // Chưa đăng nhập lần nào
                    Password = BCrypt.Net.BCrypt.HashPassword(command.Password),  // Hash password bằng BCrypt
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Bước 9: INSERT user vào bảng users trong database
                await connection.InsertAsync(user, transaction);
                logger.LogDebug("User inserted into database: {UserId}", user.Id);

                // Bước 10: Ghi log audit trail vào bảng audit_logs
                // Lưu lại hành động CREATE_USER để theo dõi
                await LogAuditAsync(connection, transaction, user);
                logger.LogDebug("Audit log created for user: {UserId}", user.Id);

                // Bước 11: Commit transaction - lưu tất cả thay đổi vào database
                // Nếu có lỗi ở các bước trước, sẽ rollback tự động
                transaction.Commit();
                logger.LogDebug("Transaction committed successfully for user: {UserId}", user.Id);

                // Bước 11.5: Lấy thông tin role để publish event
                var role = await connection.GetAsync<Roles>(roleId);
                if (role == null)
                {
                    logger.LogWarning("Role not found with ID {RoleId} for user {UserId}", roleId, user.Id);
                }

                // Bước 11.6: Publish UserCreatedEvent to RabbitMQ for other services
                // Shifts.API sẽ nhận event này và tạo cache cho manager/guard
                if (role != null)
                {
                    try
                    {
                        await eventPublisher.PublishUserCreatedAsync(user, role, cancellationToken);
                        logger.LogInformation("UserCreatedEvent published successfully for user: {UserId}", user.Id);
                    }
                    catch (Exception eventEx)
                    {
                        // Log lỗi nhưng không throw - user đã được tạo thành công
                        logger.LogError(eventEx, "Failed to publish UserCreatedEvent for user {UserId}, but user was created successfully", user.Id);
                    }
                }

                // Bước 12: Gửi email chào mừng cho user mới
                // Chạy riêng biệt, không ảnh hưởng đến việc tạo user nếu thất bại
                try
                {
                    await emailHandler.SendWelcomeEmailAsync(user.FullName, user.Email, command.Password);
                    logger.LogInformation("Welcome email sent successfully to {Email}", user.Email);
                }
                catch (Exception emailEx)
                {
                    // Log lỗi nhưng không throw exception
                    // User vẫn được tạo thành công dù email thất bại
                    logger.LogError(emailEx, "Failed to send welcome email to {Email}, but user was created successfully", user.Email);
                }

                logger.LogInformation("User created successfully: {Email}, FirebaseUid: {FirebaseUid}",
                    user.Email, user.FirebaseUid);

                // Bước 13: Trả về kết quả với thông tin user vừa tạo
                var result = new CreateUserResult(user.Id, user.FirebaseUid, user.Email);
                return result;
            }
            catch
            {
                // Rollback transaction nếu có lỗi bất kỳ
                // Đảm bảo database không bị corrupted
                transaction.Rollback();
                logger.LogWarning("Transaction rolled back due to error");
                throw;
            }
        }
        catch (FirebaseAuthException ex)
        {
            // Xử lý riêng lỗi từ Firebase (email đã tồn tại, password yếu, etc.)
            logger.LogError(ex, "Firebase error creating user: {Email}", command.Email);
            throw new InvalidOperationException($"Failed to create Firebase user: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            // Xử lý các lỗi khác
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