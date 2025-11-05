using FluentValidation;

namespace Users.API.UsersHandler.UpdatePassword;

public class UpdatePasswordValidator : AbstractValidator<UpdatePasswordCommand>
{
    public UpdatePasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.OldPassword)
            .NotEmpty().WithMessage("Current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(6).WithMessage("New password must be at least 6 characters")
            .MaximumLength(100).WithMessage("New password must not exceed 100 characters");

        RuleFor(x => x.RetypePassword)
            .NotEmpty().WithMessage("Retype password is required")
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match");

        RuleFor(x => x)
            .Must(x => x.NewPassword != x.OldPassword)
            .WithMessage("New password must be different from current password")
            .When(x => !string.IsNullOrEmpty(x.NewPassword) && !string.IsNullOrEmpty(x.OldPassword));
    }
}