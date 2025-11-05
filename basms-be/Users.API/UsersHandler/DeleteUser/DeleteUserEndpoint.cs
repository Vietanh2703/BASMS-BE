namespace Users.API.UsersHandler.DeleteUser;

public class DeleteUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/users/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeleteUserCommand(id);
            var result = await sender.Send(command);

            if (!result.IsSuccess)
            {
                return Results.NotFound(new { message = result.Message });
            }

            return Results.Ok(result);
        })
        .WithTags("Users")
        .WithName("DeleteUser")
        .Produces<DeleteUserResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Delete user")
        .WithDescription("Soft deletes a user by setting IsDeleted flag to true");
    }
}