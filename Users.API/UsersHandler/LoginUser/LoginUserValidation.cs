namespace Users.API.UsersHandler.LoginUser;

public class LoginUserValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .When(x => string.IsNullOrEmpty(x.GoogleIdToken));

        RuleFor(x => x.GoogleIdToken)
            .NotEmpty().WithMessage("Google ID Token is required")
            .When(x => string.IsNullOrEmpty(x.Password));
    }
}

