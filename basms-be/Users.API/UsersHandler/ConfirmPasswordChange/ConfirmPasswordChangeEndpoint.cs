namespace Users.API.UsersHandler.ConfirmPasswordChange;

public record ConfirmPasswordChangeRequest(
    string Email,
    string OtpCode
);

public class ConfirmPasswordChangeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/password/confirm", async ([FromBody] ConfirmPasswordChangeRequest request, ISender sender) =>
        {
            var command = new ConfirmPasswordChangeCommand(
                Email: request.Email,
                OtpCode: request.OtpCode
            );

            var result = await sender.Send(command);

            if (!result.Success)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.Ok(result);
        })
        .WithTags("Users")
        .WithName("ConfirmPasswordChange")
        .Produces<ConfirmPasswordChangeResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Confirm password change with OTP");
    }
}

