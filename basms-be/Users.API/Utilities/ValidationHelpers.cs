using FluentValidation;

namespace Users.API.Utilities;

/// <summary>
/// Reusable validation rules để tránh lặp code trong validators
/// </summary>
public static class ValidationHelpers
{
    /// <summary>
    /// Email validation rule (reusable)
    /// </summary>
    public static IRuleBuilderOptions<T, string?> EmailValidation<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        bool isRequired = true)
    {
        if (isRequired)
        {
            return ruleBuilder
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format")
                .MaximumLength(255).WithMessage("Email must not exceed 255 characters");
        }

        return ruleBuilder
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(ruleBuilder.GetType().GetProperty("PropertyName")?.GetValue(ruleBuilder) as string));
    }

    /// <summary>
    /// Phone number validation rule (reusable)
    /// </summary>
    public static IRuleBuilderOptions<T, string?> PhoneValidation<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Matches(@"^\+?[1-9]\d{1,14}$")
            .WithMessage("Invalid phone number format (E.164 format expected)");
    }

    /// <summary>
    /// Password validation rule (reusable)
    /// </summary>
    public static IRuleBuilderOptions<T, string> PasswordValidation<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        int minLength = 6,
        int maxLength = 100)
    {
        return ruleBuilder
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(minLength).WithMessage($"Password must be at least {minLength} characters")
            .MaximumLength(maxLength).WithMessage($"Password must not exceed {maxLength} characters");
    }

    /// <summary>
    /// GUID validation rule (reusable)
    /// </summary>
    public static IRuleBuilderOptions<T, Guid> GuidValidation<T>(
        this IRuleBuilder<T, Guid> ruleBuilder,
        string fieldName = "ID")
    {
        return ruleBuilder
            .NotEmpty().WithMessage($"{fieldName} is required")
            .NotEqual(Guid.Empty).WithMessage($"{fieldName} must be a valid GUID");
    }

    /// <summary>
    /// Birth day validation (1-31)
    /// </summary>
    public static IRuleBuilderOptions<T, int?> BirthDayValidation<T>(
        this IRuleBuilder<T, int?> ruleBuilder)
    {
        return ruleBuilder
            .InclusiveBetween(1, 31)
            .WithMessage("Birth day must be between 1 and 31");
    }

    /// <summary>
    /// Birth month validation (1-12)
    /// </summary>
    public static IRuleBuilderOptions<T, int?> BirthMonthValidation<T>(
        this IRuleBuilder<T, int?> ruleBuilder)
    {
        return ruleBuilder
            .InclusiveBetween(1, 12)
            .WithMessage("Birth month must be between 1 and 12");
    }

    /// <summary>
    /// Birth year validation (1900 - current year)
    /// </summary>
    public static IRuleBuilderOptions<T, int?> BirthYearValidation<T>(
        this IRuleBuilder<T, int?> ruleBuilder)
    {
        var currentYear = DateTime.Now.Year;
        return ruleBuilder
            .InclusiveBetween(1900, currentYear)
            .WithMessage($"Birth year must be between 1900 and {currentYear}");
    }

    /// <summary>
    /// Enum validation rule (reusable)
    /// </summary>
    public static IRuleBuilderOptions<T, string?> EnumValidation<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        string[] allowedValues,
        string fieldName = "Value")
    {
        return ruleBuilder
            .Must(value => value == null || allowedValues.Contains(value))
            .WithMessage($"{fieldName} must be one of: {string.Join(", ", allowedValues)}");
    }

    /// <summary>
    /// Auth provider validation
    /// </summary>
    public static IRuleBuilderOptions<T, string> AuthProviderValidation<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        var allowedProviders = new[] { "email", "google", "facebook", "apple" };
        return ruleBuilder
            .NotEmpty().WithMessage("Auth provider is required")
            .Must(x => allowedProviders.Contains(x))
            .WithMessage($"Auth provider must be one of: {string.Join(", ", allowedProviders)}");
    }

    /// <summary>
    /// User status validation
    /// </summary>
    public static IRuleBuilderOptions<T, string?> UserStatusValidation<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        var allowedStatuses = new[] { "active", "inactive", "suspended", "deleted" };
        return ruleBuilder.EnumValidation(allowedStatuses, "Status");
    }
}
