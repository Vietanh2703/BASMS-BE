namespace Users.API.UsersHandler.CreateUser;

/// <summary>
/// Validator cho CreateUserCommand - Sử dụng ValidationHelpers để giảm code lặp lại
/// </summary>
public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        // Identity Number - specific validation
        RuleFor(x => x.IdentityNumber)
            .NotEmpty().WithMessage("Identity number is required")
            .Length(12).WithMessage("Identity number must be 12 characters");

        // Email - using reusable helper
        RuleFor(x => x.Email)
            .EmailValidation(isRequired: true);

        // Password - using reusable helper
        RuleFor(x => x.Password)
            .PasswordValidation(minLength: 6, maxLength: 100);

        // Full Name - specific validation
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required")
            .MaximumLength(255).WithMessage("Full name must not exceed 255 characters");

        // Phone - using reusable helper
        RuleFor(x => x.Phone)
            .PhoneValidation();

        // Birth Day - using reusable helper
        RuleFor(x => x.BirthDay)
            .BirthDayValidation();

        // Birth Month - using reusable helper
        RuleFor(x => x.BirthMonth)
            .BirthMonthValidation();

        // Birth Year - using reusable helper
        RuleFor(x => x.BirthYear)
            .BirthYearValidation();

        // Auth Provider - using reusable helper
        RuleFor(x => x.AuthProvider)
            .AuthProviderValidation();
    }
}
