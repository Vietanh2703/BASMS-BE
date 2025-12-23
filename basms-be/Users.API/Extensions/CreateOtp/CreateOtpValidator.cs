namespace Users.API.UsersHandler.CreateOtp;

public class CreateOtpValidator : AbstractValidator<CreateOtpCommand>
{
    public CreateOtpValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Purpose)
            .NotEmpty().WithMessage("Purpose is required")
            .Must(x => new[] { "login", "verify_email", "reset_password" }.Contains(x))
            .WithMessage("Purpose must be one of: login, verify_email, reset_password");
    }
}

