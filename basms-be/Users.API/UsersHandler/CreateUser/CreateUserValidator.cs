namespace Users.API.UsersHandler.CreateUser;

public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters")
            .MaximumLength(100).WithMessage("Password must not exceed 100 characters");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required")
            .MaximumLength(255).WithMessage("Full name must not exceed 255 characters");

        RuleFor(x => x.Phone)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.BirthDay)
            .InclusiveBetween(1, 31).WithMessage("Birth day must be between 1 and 31")
            .When(x => x.BirthDay.HasValue);

        RuleFor(x => x.BirthMonth)
            .InclusiveBetween(1, 12).WithMessage("Birth month must be between 1 and 12")
            .When(x => x.BirthMonth.HasValue);

        RuleFor(x => x.BirthYear)
            .InclusiveBetween(1900, DateTime.Now.Year).WithMessage($"Birth year must be between 1900 and {DateTime.Now.Year}")
            .When(x => x.BirthYear.HasValue);

        RuleFor(x => x.AuthProvider)
            .NotEmpty().WithMessage("Auth provider is required")
            .Must(x => new[] { "email", "google", "facebook", "apple" }.Contains(x))
            .WithMessage("Auth provider must be one of: email, google, facebook, apple");
    }
}
