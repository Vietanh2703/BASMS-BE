using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Users.API.UsersHandler.UpdatePassword;

public record UpdatePasswordRequest(
    string Email,
    string OldPassword,
    string NewPassword,
    string RetypePassword
);

public class UpdatePasswordEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/users/password/update", async ([FromBody] UpdatePasswordRequest request, ISender sender) =>
        {
            var command = new UpdatePasswordCommand(
                Email: request.Email,
                OldPassword: request.OldPassword,
                NewPassword: request.NewPassword,
                RetypePassword: request.RetypePassword
            );

            var result = await sender.Send(command);

            return Results.Ok(result);
        })
        .WithTags("Users")
        .WithName("UpdatePassword")
        .Produces<UpdatePasswordResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Initiate password update")
        .WithDescription("Validates old password and sends OTP to email for confirmation before updating password");
    }
}