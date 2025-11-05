namespace Users.API.UsersHandler.UpdateUser;

public record UpdateUserRequest(
    string? FullName,
    string? Email,
    string? Phone,
    string? Address,
    int? BirthDay,
    int? BirthMonth,
    int? BirthYear,
    Guid? RoleId,
    string? AvatarUrl,
    string? Status
);

public class UpdateUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/users/{id:guid}", async (Guid id, [FromBody] UpdateUserRequest request, ISender sender) =>
        {
            var command = new UpdateUserCommand(
                Id: id,
                FullName: request.FullName,
                Email: request.Email,
                Phone: request.Phone,
                Address: request.Address,
                BirthDay: request.BirthDay,
                BirthMonth: request.BirthMonth,
                BirthYear: request.BirthYear,
                RoleId: request.RoleId,
                AvatarUrl: request.AvatarUrl,
                Status: request.Status
            );

            var result = await sender.Send(command);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Users")
        .WithName("UpdateUser")
        .Produces<UpdateUserResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Update user")
        .WithDescription("Updates user information in both database and Firebase Authentication (if email or phone changed)");
    }
}