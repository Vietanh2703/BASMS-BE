namespace Users.API.UsersHandler.UpdateUser;

public class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("User ID is required")
            .NotEqual(Guid.Empty).WithMessage("User ID must be a valid GUID");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.FullName)
            .MaximumLength(255).WithMessage("Full name must not exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.FullName));

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

        RuleFor(x => x.Status)
            .Must(x => new[] { "active", "inactive", "suspended", "deleted" }.Contains(x))
            .WithMessage("Status must be one of: active, inactive, suspended, deleted")
            .When(x => !string.IsNullOrEmpty(x.Status));
    }
}

