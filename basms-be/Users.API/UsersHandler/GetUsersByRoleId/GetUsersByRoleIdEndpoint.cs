namespace Users.API.UsersHandler.GetUsersByRoleId;

/// <summary>
/// Endpoint để lấy danh sách users theo roleId
/// </summary>
public class GetUsersByRoleIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users/by-role/{roleId:guid}", async (
            Guid roleId,
            ISender sender,
            ILogger<GetUsersByRoleIdEndpoint> logger,
            HttpContext httpContext) =>
        {
            logger.LogInformation(
                "GetUsersByRoleIdEndpoint HIT - Method: {Method}, Path: {Path}, RoleId: {RoleId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                roleId);

            var query = new GetUsersByRoleIdQuery(roleId);
            var result = await sender.Send(query);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    users = result.Users,
                    count = result.Users.Count(),
                    roleId = roleId
                },
                message = $"Retrieved {result.Users.Count()} users with roleId {roleId}"
            });
        })
        .RequireAuthorization()
        .WithTags("Users")
        .WithName("GetUsersByRoleId")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get users by role ID")
        .WithDescription(@"
            Returns a list of users with the specified roleId.

            Query Parameters:
            - roleId (guid): The role ID to filter users by

            Filters:
            - Only non-deleted users (IsDeleted = false)
            - Only users matching the specified roleId

            Response includes full user details:
            - Id, FirebaseUid, Email, EmailVerified
            - FullName, AvatarUrl, Phone, Address
            - BirthDay, BirthMonth, BirthYear
            - RoleId, RoleName
            - AuthProvider, Status
            - LastLoginAt, LoginCount, IsActive
            - CreatedAt

            Example:
            GET /api/users/by-role/ddbd5fad-ba6e-11f0-bcac-00155dca8f48
        ");
    }
}
