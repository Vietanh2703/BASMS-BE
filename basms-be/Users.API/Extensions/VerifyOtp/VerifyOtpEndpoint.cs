namespace Users.API.UsersHandler.VerifyOtp;

public record VerifyOtpRequest(
    string Email,
    string OtpCode,
    string Purpose
);

public class VerifyOtpEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/otp/verify", async ([FromBody] VerifyOtpRequest request, ISender sender) =>
            {
                var command = new VerifyOtpCommand(
                    Email: request.Email,
                    OtpCode: request.OtpCode,
                    Purpose: request.Purpose
                );

                var result = await sender.Send(command);

                if (!result.IsValid)
                {
                    return Results.BadRequest(new { message = result.Message });
                }

                return Results.Ok(result);
            })
            .WithTags("OTP")
            .WithName("VerifyOtp")
            .Produces<VerifyOtpResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Verify OTP code")
            .WithDescription("Verifies the OTP code. Account will be locked after 5 failed attempts.");
    }
}