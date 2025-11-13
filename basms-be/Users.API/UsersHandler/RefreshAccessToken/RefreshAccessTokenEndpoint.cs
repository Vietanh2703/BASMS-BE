namespace Users.API.UsersHandler.RefreshAccessToken;

// Request DTO
public record RefreshAccessTokenRequest(
    string RefreshToken
);

// Response DTO
public record RefreshAccessTokenResponse(
    Guid UserId,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    DateTime RefreshTokenExpiry
);

public class RefreshAccessTokenEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/refresh-token", async (RefreshAccessTokenRequest request, ISender sender) =>
            {
                var command = new RefreshAccessTokenCommand(
                    RefreshToken: request.RefreshToken
                );

                var result = await sender.Send(command);
                var response = result.Adapt<RefreshAccessTokenResponse>();

                return Results.Ok(response);
            })
            .WithTags("Authentication")
            .WithName("RefreshAccessToken")
            .Produces<RefreshAccessTokenResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Refresh access token")
            .WithDescription("Refreshes access token using refresh token and returns new JWT tokens");
    }
}