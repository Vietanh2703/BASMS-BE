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
        
        app.MapPost("/api/users/reset-password/request-otp", async (RequestResetPasswordRequest request, ISender sender) =>
        {
            var command = new RequestResetPasswordCommand(
                Email: request.Email
            );

            var result = await sender.Send(command);
            var response = result.Adapt<RequestResetPasswordResponse>();

            return Results.Ok(response);
        })
        .AllowAnonymous()
        .WithTags("Users")
        .WithName("RequestResetPassword")
        .Produces<RequestResetPasswordResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Request password reset OTP")
        .WithDescription("Sends an OTP code to the user's email. Step 2 of 4: Validate Email → Request OTP → Verify OTP → Reset Password");


        app.MapPost("/api/users/reset-password", async (CompleteResetPasswordRequest request, ISender sender) =>
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
        .AllowAnonymous()
        .WithTags("Users")
        .WithName("ResetPassword")
        .Produces<CompleteResetPasswordResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Reset password")
        .WithDescription("Updates the user's password after successful OTP verification. Step 4 of 4: Validate Email → Request OTP → Verify OTP → Reset Password. Accepts email, new password, and confirm password.");
    }
}