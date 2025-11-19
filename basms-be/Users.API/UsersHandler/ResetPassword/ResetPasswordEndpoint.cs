namespace Users.API.UsersHandler.ResetPassword;

// Step 1: Request DTO - Request reset password
public record RequestResetPasswordRequest(
    string Email
);

public record RequestResetPasswordResponse(
    bool Success,
    string Message,
    DateTime? ExpiresAt = null
);

// Step 2: Request DTO - Verify OTP
public record VerifyResetPasswordOtpRequest(
    string Email,
    string OtpCode
);

public record VerifyResetPasswordOtpResponse(
    bool IsValid,
    string Message,
    Guid? UserId = null
);

// Step 3: Request DTO - Complete reset password
public record CompleteResetPasswordRequest(
    string Email,
    string NewPassword,
    string ConfirmPassword
);

public record CompleteResetPasswordResponse(
    bool Success,
    string Message
);

public class ResetPasswordEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Step 1: Request reset password - Send OTP via email
        app.MapPost("/api/users/reset-password/request", async (RequestResetPasswordRequest request, ISender sender) =>
        {
            var command = new RequestResetPasswordCommand(
                Email: request.Email
            );

            var result = await sender.Send(command);
            var response = result.Adapt<RequestResetPasswordResponse>();

            return Results.Ok(response);
        })
        .WithTags("Users")
        .WithName("RequestResetPassword")
        .Produces<RequestResetPasswordResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Request password reset")
        .WithDescription("Sends an OTP code to the user's email to verify password reset request");

        // Step 2: Verify OTP for password reset
        app.MapPost("/api/users/reset-password/verify-otp", async (VerifyResetPasswordOtpRequest request, ISender sender) =>
        {
            var command = new VerifyResetPasswordOtpCommand(
                Email: request.Email,
                OtpCode: request.OtpCode
            );

            var result = await sender.Send(command);
            var response = result.Adapt<VerifyResetPasswordOtpResponse>();

            return Results.Ok(response);
        })
        .WithTags("Users")
        .WithName("VerifyResetPasswordOtp")
        .Produces<VerifyResetPasswordOtpResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Verify OTP for password reset")
        .WithDescription("Verifies the OTP code sent to user's email before allowing password reset");

        // Step 3: Complete password reset with new password
        app.MapPost("/api/users/reset-password/complete", async (CompleteResetPasswordRequest request, ISender sender) =>
        {
            var command = new CompleteResetPasswordCommand(
                Email: request.Email,
                NewPassword: request.NewPassword,
                ConfirmPassword: request.ConfirmPassword
            );

            var result = await sender.Send(command);
            var response = result.Adapt<CompleteResetPasswordResponse>();

            return Results.Ok(response);
        })
        .WithTags("Users")
        .WithName("CompleteResetPassword")
        .Produces<CompleteResetPasswordResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Complete password reset")
        .WithDescription("Updates the user's password after successful OTP verification");
    }
}