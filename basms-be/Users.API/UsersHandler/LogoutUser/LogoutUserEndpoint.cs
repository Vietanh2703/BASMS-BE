namespace Users.API.UsersHandler.LogoutUser;

public record LogoutUserResponse(bool Success, string Message);

public class LogoutUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/users/logout", [Authorize] async (ISender sender) =>
        {
            try
            {
                var command = new LogoutUserCommand();
                var result = await sender.Send(command);
                var response = result.Adapt<LogoutUserResponse>();

                return Results.Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Logout Failed",
                    detail: ex.Message
                );
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: ex.Message
                );
            }
        })
        .WithTags("Users")
        .WithName("LogoutUser")
        .Produces<LogoutUserResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Logout user")
        .WithDescription("Revokes all active tokens and deactivates user session")
        .RequireAuthorization();
    }
}
