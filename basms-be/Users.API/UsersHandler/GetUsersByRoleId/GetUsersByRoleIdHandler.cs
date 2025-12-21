namespace Users.API.UsersHandler.GetUsersByRoleId;

/// <summary>
/// Query để lấy danh sách users theo roleId
/// </summary>
public record GetUsersByRoleIdQuery(Guid RoleId) : IQuery<GetUsersByRoleIdResult>;

/// <summary>
/// Result chứa danh sách users
/// </summary>
public record GetUsersByRoleIdResult(IEnumerable<UserDto> Users);

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
/// Handler để lấy danh sách users theo roleId
/// </summary>
internal class GetUsersByRoleIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetUsersByRoleIdHandler> logger)
    : IQueryHandler<GetUsersByRoleIdQuery, GetUsersByRoleIdResult>
{
    public async Task<GetUsersByRoleIdResult> Handle(
        GetUsersByRoleIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting users with RoleId: {RoleId}", request.RoleId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            var users = await connection.GetAllAsync<Models.Users>();
            var roles = await connection.GetAllAsync<Roles>();

            var userDtos = users
                .Where(u => !u.IsDeleted && u.RoleId == request.RoleId)
                .Select(u =>
                {
                    var role = roles.FirstOrDefault(r => r.Id == u.RoleId);
                    return new UserDto(
                        Id: u.Id,
                        FirebaseUid: u.FirebaseUid,
                        Email: u.Email,
                        EmailVerified: u.EmailVerified,
                        FullName: u.FullName,
                        AvatarUrl: u.AvatarUrl,
                        Phone: u.Phone,
                        Address: u.Address,
                        BirthDay: u.BirthDay,
                        BirthMonth: u.BirthMonth,
                        BirthYear: u.BirthYear,
                        RoleId: u.RoleId,
                        RoleName: role?.Name ?? "Unknown",
                        AuthProvider: u.AuthProvider,
                        Status: u.Status,
                        LastLoginAt: u.LastLoginAt,
                        LoginCount: u.LoginCount,
                        IsActive: u.IsActive,
                        CreatedAt: u.CreatedAt
                    );
                })
                .OrderByDescending(u => u.CreatedAt)
                .ToList();

            logger.LogInformation(
                "Successfully retrieved {Count} users with RoleId: {RoleId}",
                userDtos.Count,
                request.RoleId);

            return new GetUsersByRoleIdResult(userDtos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting users by RoleId: {RoleId}", request.RoleId);
            throw;
        }
    }
}
