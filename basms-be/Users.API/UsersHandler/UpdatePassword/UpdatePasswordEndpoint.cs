namespace Users.API.UsersHandler.UpdatePassword;

public record UpdatePasswordRequest(
    string Email,
    string NewPassword,
    string ConfirmPassword
);

public class UpdatePasswordEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/password/update", async ([FromBody] UpdatePasswordRequest request, ISender sender) =>
            {
                var command = new UpdatePasswordCommand(
                    Email: request.Email,
                    NewPassword: request.NewPassword,
                    ConfirmPassword: request.ConfirmPassword
                );

                var result = await sender.Send(command);

                return Results.Ok(result);
            })
            .WithTags("Users")
            .WithName("UpdatePassword")
            .Produces<UpdatePasswordResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Update user password")
            .WithDescription("Updates user password directly without OTP verification");
    }
}