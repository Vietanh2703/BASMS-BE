namespace Users.API.Extensions.UpdateOtp;

public record UpdateOtpRequest(
    string Email,
    string Purpose
);

public class UpdateOtpEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/otp/refresh-otp", async ([FromBody] UpdateOtpRequest request, ISender sender) =>
            {
                var command = new UpdateOtpCommand(
                    Email: request.Email,
                    Purpose: request.Purpose
                );

                var result = await sender.Send(command);

                if (!result.Success)
                {
                    return Results.NotFound(new { message = result.Message });
                }

                return Results.Ok(result);
            })
            .WithTags("OTP")
            .WithName("RefreshOtp")
            .Produces<UpdateOtpResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Refresh OTP")
            .WithDescription("Refresh OTP - Reset attempts to maximum and extend expiry time by 10 minutes.");
    }
}