namespace Users.API.UsersHandler.GetUserById;

public record GetUserByIdQuery(Guid Id) : IQuery<GetUserByIdResult>;

public record GetUserByIdResult(UserDetailDto User);

public record UserDetailDto(
    Guid Id,
    string FirebaseUid,
    string Email,
    bool EmailVerified,              
    DateTime? EmailVerifiedAt,   
    string FullName,
    string? AvatarUrl,
    string? Phone,
    string? Address,
    int? BirthDay,
    int? BirthMonth,
    int? BirthYear,
    Guid RoleId,
    string RoleName,                
    string RoleDisplayName,         
    string AuthProvider,           
    string Status,                 
    DateTime? LastLoginAt,        
    int LoginCount,                 
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
            
            using var connection = await connectionFactory.CreateConnectionAsync();
            
            var users = await connection.GetAllAsync<Models.Users>();
            var user = users.FirstOrDefault(u => u.Id == request.Id && !u.IsDeleted);

            if (user == null)
            {
                logger.LogWarning("User not found with ID: {UserId}", request.Id);
                throw new InvalidOperationException($"User with ID {request.Id} not found");
            }
            
            var roles = await connection.GetAllAsync<Roles>();
            var role = roles.FirstOrDefault(r => r.Id == user.RoleId);
            
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
                RoleName: role?.Name ?? "Unknown",             
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
            
            return new GetUserByIdResult(userDetailDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user by ID: {UserId}", request.Id);
            throw;
        }
    }
}