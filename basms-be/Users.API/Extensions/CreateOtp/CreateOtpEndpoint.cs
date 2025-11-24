namespace Users.API.Extensions.CreateOtp;

public record CreateOtpRequest(
    string Email,
    string Purpose // login, verify_email, reset_password
);

public class CreateOtpEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/otp/create", async ([FromBody] CreateOtpRequest request, ISender sender) =>
        {
            var command = new CreateOtpCommand(
                Email: request.Email,
                Purpose: request.Purpose
            );

            var result = await sender.Send(command);

            return Results.Ok(result);
        })
        .WithTags("OTP")
        .WithName("CreateOtp")
        .Produces<CreateOtpResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Create OTP code")
        .WithDescription("Generates a 6-digit OTP code valid for 10 minutes and sends it via email");
    }
}