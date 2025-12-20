namespace Users.API.UsersHandler.LoginUser;

public record LoginUserRequest(
    string Email,
    string Password
);

public record GoogleLoginRequest(
    string GoogleIdToken
);

public record LoginUserResponse(
    Guid UserId,
    string Email,
    string FullName,
    Guid RoleId,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    DateTime RefreshTokenExpiry,
    DateTime SessionExpiry
);

public class LoginUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Email/Password Login
        app.MapPost("/api/users/login", async (LoginUserRequest request, ISender sender) =>
        {
            var command = new LoginUserCommand(
                Email: request.Email,
                Password: request.Password,
                GoogleIdToken: null
            );

            var result = await sender.Send(command);
            var response = result.Adapt<LoginUserResponse>();

            return Results.Ok(response);
        })
        .WithTags("Users")
        .WithName("LoginUser")
        .Produces<LoginUserResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Login with email and password")
        .WithDescription("Authenticates user with email/password and returns JWT tokens");

        // Google Login
        app.MapPost("/users/login/google", async (GoogleLoginRequest request, ISender sender) =>
        {
            var command = new LoginUserCommand(
                Email: string.Empty,
                Password: null,
                GoogleIdToken: request.GoogleIdToken
            );

            var result = await sender.Send(command);
            var response = result.Adapt<LoginUserResponse>();

            return Results.Ok(response);
        })
        .WithTags("Users")
        .WithName("GoogleLogin")
        .Produces<LoginUserResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Login with Google")
        .WithDescription("Authenticates user with Google ID token and returns JWT tokens");
    }
}
