namespace Users.API.UsersHandler.GetUserByEmail;

/// <summary>
/// Endpoint để lấy user theo email
/// </summary>
public class GetUserByEmailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users/by-email/{email}", async (
            string email,
            ISender sender,
            ILogger<GetUserByEmailEndpoint> logger,
            HttpContext httpContext) =>
        {
            logger.LogInformation(
                "GetUserByEmailEndpoint HIT - Method: {Method}, Path: {Path}, Email: {Email}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                email);

            var query = new GetUserByEmailQuery(email);
            var result = await sender.Send(query);

            if (!result.Success)
            {
                return Results.NotFound(new
                {
                    success = false,
                    message = result.ErrorMessage,
                    email = email
                });
            }

            return Results.Ok(new
            {
                success = true,
                data = result.User,
                message = $"User with email '{email}' found"
            });
        })
        .RequireAuthorization()
        .WithTags("Users")
        .WithName("GetUserByEmail")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get user by email address");
    }
}
