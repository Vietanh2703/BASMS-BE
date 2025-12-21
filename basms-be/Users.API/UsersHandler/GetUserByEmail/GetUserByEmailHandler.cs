namespace Users.API.UsersHandler.GetUserByEmail;

/// <summary>
/// Query để lấy user theo email
/// </summary>
public record GetUserByEmailQuery(string Email) : IQuery<GetUserByEmailResult>;

/// <summary>
/// Result chứa thông tin user
/// </summary>
public record GetUserByEmailResult
{
    public bool Success { get; init; }
    public UserDto? User { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO cho User
/// </summary>
public record UserDto(
    Guid Id,
    string FirebaseUid,
    string Email,
    bool EmailVerified,
    string FullName,
    string? AvatarUrl,
    string? Phone,
    string? Address,
    int? BirthDay,
    int? BirthMonth,
    int? BirthYear,
    Guid RoleId,
    string RoleName,
    string AuthProvider,
    string Status,
    DateTime? LastLoginAt,
    int LoginCount,
    bool IsActive,
    DateTime CreatedAt
);

/// <summary>
/// Handler để lấy user theo email
/// </summary>
internal class GetUserByEmailHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetUserByEmailHandler> logger)
    : IQueryHandler<GetUserByEmailQuery, GetUserByEmailResult>
{
    public async Task<GetUserByEmailResult> Handle(
        GetUserByEmailQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting user with Email: {Email}", request.Email);

            using var connection = await connectionFactory.CreateConnectionAsync();

            var users = await connection.GetAllAsync<Models.Users>();
            var roles = await connection.GetAllAsync<Roles>();

            var user = users.FirstOrDefault(u =>
                !u.IsDeleted &&
                u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                logger.LogWarning("User not found with Email: {Email}", request.Email);
                return new GetUserByEmailResult
                {
                    Success = false,
                    ErrorMessage = $"User with email '{request.Email}' not found"
                };
            }

            var role = roles.FirstOrDefault(r => r.Id == user.RoleId);

            var userDto = new UserDto(
                Id: user.Id,
                FirebaseUid: user.FirebaseUid,
                Email: user.Email,
                EmailVerified: user.EmailVerified,
                FullName: user.FullName,
                AvatarUrl: user.AvatarUrl,
                Phone: user.Phone,
                Address: user.Address,
                BirthDay: user.BirthDay,
                BirthMonth: user.BirthMonth,
                BirthYear: user.BirthYear,
                RoleId: user.RoleId,
                RoleName: role?.Name ?? "Unknown",
                AuthProvider: user.AuthProvider,
                Status: user.Status,
                LastLoginAt: user.LastLoginAt,
                LoginCount: user.LoginCount,
                IsActive: user.IsActive,
                CreatedAt: user.CreatedAt
            );

            logger.LogInformation(
                "Successfully retrieved user with Email: {Email}, UserId: {UserId}",
                request.Email,
                user.Id);

            return new GetUserByEmailResult
            {
                Success = true,
                User = userDto
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user by Email: {Email}", request.Email);
            return new GetUserByEmailResult
            {
                Success = false,
                ErrorMessage = $"Error retrieving user: {ex.Message}"
            };
        }
    }
}
