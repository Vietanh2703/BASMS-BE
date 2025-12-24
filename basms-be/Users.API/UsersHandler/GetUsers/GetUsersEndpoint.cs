namespace Users.API.UsersHandler.GetUsers;

public class GetUsersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users", async (ISender sender, ILogger<GetUsersEndpoint> logger, HttpContext httpContext) =>
        {
            // Log để debug routing issue
            logger.LogInformation("GetUsersEndpoint HIT - Method: {Method}, Path: {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

            var query = new GetUsersQuery();
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .AddEndpointFilter(new RoleAuthorizationFilter("ddbd5fad-ba6e-11f0-bcac-00155dca8f48"))
        .WithTags("Users")
        .WithName("GetUsers")
        .Produces<GetUsersResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get all users");
    }
}