using FluentValidation;

namespace Users.API.UsersHandler.ConfirmPasswordChange;

public class ConfirmPasswordChangeValidator : AbstractValidator<ConfirmPasswordChangeCommand>
{
    public ConfirmPasswordChangeValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("OTP code is required")
            .Length(6).WithMessage("OTP code must be 6 characters");
    }
}