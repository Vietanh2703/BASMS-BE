namespace Users.API.UsersHandler.UpdateUser;

/// <summary>
/// Validator cho UpdateUserCommand - Sử dụng ValidationHelpers để giảm code lặp lại
/// </summary>
public class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        // User ID - using reusable helper
        RuleFor(x => x.Id)
            .GuidValidation("User ID");

        // Email - using reusable helper (not required for update)
        RuleFor(x => x.Email)
            .EmailValidation(isRequired: false)
            .When(x => !string.IsNullOrEmpty(x.Email));

        // Full Name - specific validation
        RuleFor(x => x.FullName)
            .MaximumLength(255).WithMessage("Full name must not exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.FullName));

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

        // Status - using reusable helper
        RuleFor(x => x.Status)
            .UserStatusValidation();
    }
}

