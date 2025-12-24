using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Users.API.UsersHandler.SendLoginEmail;

public class SendLoginEmailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/send-login-email", async (
            [FromBody] SendLoginEmailRequest request,
            ISender sender,
            ILogger<SendLoginEmailEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "POST /api/users/send-login-email - Email: {Email}, Phone: {Phone}",
                request.Email,
                request.PhoneNumber);

            var command = new SendLoginEmailCommand(
                Email: request.Email,
                PhoneNumber: request.PhoneNumber
            );

            var result = await sender.Send(command, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to send login email: {Error}",
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "Login email sent successfully to {Email}",
                result.Email);

            return Results.Ok(new
            {
                success = true,
                email = result.Email,
                fullName = result.FullName,
                emailSent = result.EmailSent,
                message = $"Login credentials have been sent to {result.Email}. Please check your email and change your password after logging in."
            });
        })
        .WithName("SendLoginEmail")
        .WithTags("Users - Authentication")
        .WithSummary("Gửi email chứa thông tin đăng nhập")
        .Produces(200)
        .Produces(400);
    }
}


public record SendLoginEmailRequest
{
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
}
