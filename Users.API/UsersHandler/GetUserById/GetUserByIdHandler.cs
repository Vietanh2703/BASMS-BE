// Handler xử lý logic lấy thông tin user theo ID
// Query database và join với bảng Roles để lấy đầy đủ thông tin
namespace Users.API.UsersHandler.GetUserById;

// Query chứa ID user cần lấy
public record GetUserByIdQuery(Guid Id) : IQuery<GetUserByIdResult>;

// Result chứa user detail DTO
public record GetUserByIdResult(UserDetailDto User);

// DTO chứa đầy đủ thông tin user bao gồm cả role
public record UserDetailDto(
    Guid Id,
    string FirebaseUid,
    string Email,
    bool EmailVerified,              // Email đã được xác thực chưa
    DateTime? EmailVerifiedAt,       // Thời điểm xác thực email
    string FullName,
    string? AvatarUrl,
    string? Phone,
    string? Address,
    int? BirthDay,
    int? BirthMonth,
    int? BirthYear,
    Guid RoleId,
    string RoleName,                 // Tên role (guard, admin, etc.)
    string RoleDisplayName,          // Tên hiển thị role
    string AuthProvider,             // Phương thức đăng ký (email/google)
    string Status,                   // Trạng thái (active/inactive/deleted)
    DateTime? LastLoginAt,           // Lần đăng nhập cuối
    int LoginCount,                  // Số lần đã đăng nhập
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

internal class GetUserByIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetUserByIdHandler> logger)
    : IQueryHandler<GetUserByIdQuery, GetUserByIdResult>
{
    public async Task<GetUserByIdResult> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting user by ID: {UserId}", request.Id);

            // Bước 1: Tạo kết nối database (không cần transaction vì chỉ đọc)
            using var connection = await connectionFactory.CreateConnectionAsync();

            // Bước 2: Lấy tất cả users và tìm theo ID
            // Filter: chưa bị xóa (IsDeleted = false)
            var users = await connection.GetAllAsync<Models.Users>();
            var user = users.FirstOrDefault(u => u.Id == request.Id && !u.IsDeleted);

            if (user == null)
            {
                logger.LogWarning("User not found with ID: {UserId}", request.Id);
                throw new InvalidOperationException($"User with ID {request.Id} not found");
            }

            // Bước 3: Lấy thông tin role của user
            // Join với bảng Roles để lấy RoleName và RoleDisplayName
            var roles = await connection.GetAllAsync<Roles>();
            var role = roles.FirstOrDefault(r => r.Id == user.RoleId);

            // Bước 4: Map entity sang DTO
            // Chuyển đổi từ Models.Users sang UserDetailDto để trả về client
            var userDetailDto = new UserDetailDto(
                Id: user.Id,
                FirebaseUid: user.FirebaseUid,
                Email: user.Email,
                EmailVerified: user.EmailVerified,
                EmailVerifiedAt: user.EmailVerifiedAt,
                FullName: user.FullName,
                AvatarUrl: user.AvatarUrl,
                Phone: user.Phone,
                Address: user.Address,
                BirthDay: user.BirthDay,
                BirthMonth: user.BirthMonth,
                BirthYear: user.BirthYear,
                RoleId: user.RoleId,
                RoleName: role?.Name ?? "Unknown",              // Hiển thị "Unknown" nếu không tìm thấy role
                RoleDisplayName: role?.DisplayName ?? "Unknown",
                AuthProvider: user.AuthProvider,
                Status: user.Status,
                LastLoginAt: user.LastLoginAt,
                LoginCount: user.LoginCount,
                IsActive: user.IsActive,
                CreatedAt: user.CreatedAt,
                UpdatedAt: user.UpdatedAt
            );

            logger.LogInformation("Successfully retrieved user: {Email}", user.Email);

            // Bước 5: Trả về kết quả
            return new GetUserByIdResult(userDetailDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user by ID: {UserId}", request.Id);
            throw;
        }
    }
}