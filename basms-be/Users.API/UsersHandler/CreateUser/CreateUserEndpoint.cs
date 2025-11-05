namespace Users.API.UsersHandler.CreateUser;

// Request DTO from client
public record CreateUserRequest(
    string Email,
    string Password,
    string FullName,
    string? Phone,
    string? Address,
    int? BirthDay,
    int? BirthMonth,
    int? BirthYear,
    Guid? RoleId,
    string? AvatarUrl,
    string AuthProvider = "email"
);

// Response DTO to client
public record CreateUserResponse(
    Guid Id,
    string FirebaseUid,
    string Email
);

public class CreateUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/users", async (CreateUserRequest request, ISender sender) =>
        {
            // Map request to command
            var command = request.Adapt<CreateUserCommand>();

            // Send command via MediatR
            var result = await sender.Send(command);

            // Map result to response
            var response = result.Adapt<CreateUserResponse>();

            // Return 201 Created with location header
            return Results.Created($"/users/{response.Id}", response);
        })
        .WithTags("Users")
        .WithName("CreateUser")
        .Produces<CreateUserResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Creates a new user")
        .WithDescription("Creates a new user account with Firebase authentication and stores in MySQL database");
    }
}