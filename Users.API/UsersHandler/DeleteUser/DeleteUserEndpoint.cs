namespace Users.API.UsersHandler.DeleteUser;

public class DeleteUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/users/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeleteUserCommand(id);
            var result = await sender.Send(command);

            if (!result.IsSuccess)
            {
                return Results.NotFound(new { message = result.Message });
            }

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .AddEndpointFilter(new RoleAuthorizationFilter("ddbd5fad-ba6e-11f0-bcac-00155dca8f48", "ddbd612f-ba6e-11f0-bcac-00155dca8f48"))
        .WithTags("Users")
        .WithName("DeleteUser")
        .Produces<DeleteUserResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Delete user")
        .WithDescription("Soft deletes a user by setting IsDeleted flag to true");
    }
}