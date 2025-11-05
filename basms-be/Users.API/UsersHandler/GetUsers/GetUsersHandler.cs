namespace Users.API.UsersHandler.GetUsers;

public record GetUsersQuery() : IQuery<GetUsersResult>;
public record GetUsersResult(IEnumerable<UserDto> Users);

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

internal class GetUsersHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetUsersHandler> logger) 
    : IQueryHandler<GetUsersQuery, GetUsersResult>
{
    public async Task<GetUsersResult> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting all users from database");

            using var connection = await connectionFactory.CreateConnectionAsync();

            // Get all users (not deleted)
            var users = await connection.GetAllAsync<Models.Users>();
            var roles = await connection.GetAllAsync<Roles>();

            // Filter out deleted users and map to DTO
            var userDtos = users
                .Where(u => !u.IsDeleted)
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

            logger.LogInformation("Successfully retrieved {Count} users", userDtos.Count);

            return new GetUsersResult(userDtos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting users from database");
            throw;
        }
    }
}