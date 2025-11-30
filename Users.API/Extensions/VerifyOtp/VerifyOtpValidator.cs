using FluentValidation;

namespace Users.API.UsersHandler.VerifyOtp;

public class VerifyOtpValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("OTP code is required")
            .Length(6).WithMessage("OTP code must be 6 characters");

        RuleFor(x => x.Purpose)
            .NotEmpty().WithMessage("Purpose is required")
            .Must(x => new[] { "login", "verify_email", "reset_password" }.Contains(x))
            .WithMessage("Purpose must be one of: login, verify_email, reset_password");
    }
}


