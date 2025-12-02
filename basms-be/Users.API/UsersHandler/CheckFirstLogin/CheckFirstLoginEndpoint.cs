namespace Users.API.UsersHandler.CheckFirstLogin;

public record CheckFirstLoginRequest(string Email);

public class CheckFirstLoginEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/check-first-login", async ([FromBody] CheckFirstLoginRequest request, ISender sender) =>
            {
                var query = new CheckFirstLoginQuery(request.Email);
                var result = await sender.Send(query);
                return Results.Ok(result);
            })
            .WithTags("Users")
            .WithName("CheckFirstLogin")
            .Produces<CheckFirstLoginResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Check if user is logging in for the first time")
            .WithDescription("Returns true if user's LoginCount is 1, false otherwise");
    }
}