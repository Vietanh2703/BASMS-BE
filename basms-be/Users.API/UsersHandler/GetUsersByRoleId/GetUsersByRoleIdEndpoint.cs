namespace Users.API.UsersHandler.GetUsersByRoleId;


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
        .WithSummary("Get users by role ID");
    }
}
