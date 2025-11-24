namespace Users.API.UsersHandler.ValidateEmail;

// Request DTO
public record ValidateEmailRequest(
    string Email
);

public record ValidateEmailResponse(
    bool IsValid,
    string Message,
    Guid? UserId = null,
    string? FullName = null
);

public class ValidateEmailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/validate-email", async (ValidateEmailRequest request, ISender sender) =>
        {
            var command = new ValidateEmailCommand(
                Email: request.Email
            );

            var result = await sender.Send(command);
            var response = result.Adapt<ValidateEmailResponse>();

            return Results.Ok(response);
        })
        .AllowAnonymous()
        .WithTags("Users")
        .WithName("ValidateEmail")
        .Produces<ValidateEmailResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Validate email address")
        .WithDescription("Checks if an email address exists in the system and is active. This is the first step before requesting a password reset.");
    }
}
