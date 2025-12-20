namespace Users.API.UsersHandler.GetUserById;

public class GetUserByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users/{id:guid}", async (Guid id, ISender sender) =>
        {
            var query = new GetUserByIdQuery(id);
            
            var result = await sender.Send(query);

            return Results.Ok(result);
        })
        .RequireAuthorization() 
        .WithTags("Users")
        .WithName("GetUserById")
        .Produces<GetUserByIdResult>(StatusCodes.Status200OK)  
        .ProducesProblem(StatusCodes.Status401Unauthorized)    
        .ProducesProblem(StatusCodes.Status404NotFound)       
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get user by ID");
    }
}