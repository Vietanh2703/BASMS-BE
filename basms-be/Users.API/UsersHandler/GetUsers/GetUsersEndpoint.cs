namespace Users.API.UsersHandler.GetUsers;

public class GetUsersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/users", async (ISender sender) =>
        {
            var query = new GetUsersQuery();
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithTags("Users")
        .WithName("GetUsers")
        .Produces<GetUsersResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get all users")
        .WithDescription("Retrieves all active users from the database with their role information");
    }
}