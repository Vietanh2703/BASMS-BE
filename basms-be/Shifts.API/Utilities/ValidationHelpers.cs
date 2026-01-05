using FluentValidation;

namespace Shifts.API.Utilities;

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
        var rule = ruleBuilder
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

        if (isRequired)
        {
            rule.NotEmpty().WithMessage("Email is required");
        }

        return rule;
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
    /// Shift type validation
    /// </summary>
    public static IRuleBuilderOptions<T, string> ShiftTypeValidation<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        var allowedShiftTypes = new[] { "REGULAR", "OVERTIME", "HOLIDAY", "WEEKEND", "NIGHT", "EMERGENCY" };
        return ruleBuilder
            .NotEmpty().WithMessage("Shift type is required")
            .Must(x => allowedShiftTypes.Contains(x))
            .WithMessage($"Shift type must be one of: {string.Join(", ", allowedShiftTypes)}");
    }

    /// <summary>
    /// Employment status validation
    /// </summary>
    public static IRuleBuilderOptions<T, string?> EmploymentStatusValidation<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        var allowedStatuses = new[] { "ACTIVE", "INACTIVE", "ON_LEAVE", "TERMINATED", "SUSPENDED" };
        return ruleBuilder.EnumValidation(allowedStatuses, "Employment status");
    }

    /// <summary>
    /// Shift status validation
    /// </summary>
    public static IRuleBuilderOptions<T, string?> ShiftStatusValidation<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        var allowedStatuses = new[] { "DRAFT", "PENDING", "APPROVED", "IN_PROGRESS", "COMPLETED", "CANCELLED" };
        return ruleBuilder.EnumValidation(allowedStatuses, "Shift status");
    }

    /// <summary>
    /// Approval status validation
    /// </summary>
    public static IRuleBuilderOptions<T, string?> ApprovalStatusValidation<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        var allowedStatuses = new[] { "PENDING", "APPROVED", "REJECTED" };
        return ruleBuilder.EnumValidation(allowedStatuses, "Approval status");
    }

    /// <summary>
    /// Certification level validation
    /// </summary>
    public static IRuleBuilderOptions<T, string?> CertificationLevelValidation<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        var allowedLevels = new[] { "I", "II", "III", "IV", "V" };
        return ruleBuilder.EnumValidation(allowedLevels, "Certification level");
    }

    /// <summary>
    /// Gender validation
    /// </summary>
    public static IRuleBuilderOptions<T, string?> GenderValidation<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        var allowedGenders = new[] { "Male", "Female", "Other" };
        return ruleBuilder.EnumValidation(allowedGenders, "Gender");
    }

    /// <summary>
    /// Contract type validation
    /// </summary>
    public static IRuleBuilderOptions<T, string?> ContractTypeValidation<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        var allowedTypes = new[] { "FULL_TIME", "PART_TIME", "CONTRACT", "TEMPORARY", "SEASONAL" };
        return ruleBuilder.EnumValidation(allowedTypes, "Contract type");
    }

    /// <summary>
    /// Date range validation (start date must be before end date)
    /// </summary>
    public static IRuleBuilderOptions<T, DateTime> DateRangeValidation<T>(
        this IRuleBuilder<T, DateTime> ruleBuilder,
        Func<T, DateTime> endDateSelector,
        string startFieldName = "Start date",
        string endFieldName = "End date")
    {
        return ruleBuilder
            .Must((model, startDate) => startDate < endDateSelector(model))
            .WithMessage($"{startFieldName} must be before {endFieldName}");
    }

    /// <summary>
    /// Future date validation
    /// </summary>
    public static IRuleBuilderOptions<T, DateTime> FutureDateValidation<T>(
        this IRuleBuilder<T, DateTime> ruleBuilder,
        string fieldName = "Date")
    {
        return ruleBuilder
            .GreaterThanOrEqualTo(DateTime.UtcNow.Date)
            .WithMessage($"{fieldName} must be in the future or today");
    }

    /// <summary>
    /// Past date validation
    /// </summary>
    public static IRuleBuilderOptions<T, DateTime> PastDateValidation<T>(
        this IRuleBuilder<T, DateTime> ruleBuilder,
        string fieldName = "Date")
    {
        return ruleBuilder
            .LessThanOrEqualTo(DateTime.UtcNow.Date)
            .WithMessage($"{fieldName} must be in the past or today");
    }

    /// <summary>
    /// TimeSpan validation (start time must be before end time)
    /// </summary>
    public static IRuleBuilderOptions<T, TimeSpan> TimeSpanRangeValidation<T>(
        this IRuleBuilder<T, TimeSpan> ruleBuilder,
        Func<T, TimeSpan> endTimeSelector,
        string startFieldName = "Start time",
        string endFieldName = "End time")
    {
        return ruleBuilder
            .Must((model, startTime) => startTime < endTimeSelector(model))
            .WithMessage($"{startFieldName} must be before {endFieldName}");
    }

    /// <summary>
    /// Positive number validation
    /// </summary>
    public static IRuleBuilderOptions<T, int> PositiveNumberValidation<T>(
        this IRuleBuilder<T, int> ruleBuilder,
        string fieldName = "Number")
    {
        return ruleBuilder
            .GreaterThan(0)
            .WithMessage($"{fieldName} must be greater than 0");
    }

    /// <summary>
    /// Non-negative number validation
    /// </summary>
    public static IRuleBuilderOptions<T, int> NonNegativeNumberValidation<T>(
        this IRuleBuilder<T, int> ruleBuilder,
        string fieldName = "Number")
    {
        return ruleBuilder
            .GreaterThanOrEqualTo(0)
            .WithMessage($"{fieldName} must be greater than or equal to 0");
    }

    /// <summary>
    /// Positive decimal validation
    /// </summary>
    public static IRuleBuilderOptions<T, decimal> PositiveDecimalValidation<T>(
        this IRuleBuilder<T, decimal> ruleBuilder,
        string fieldName = "Amount")
    {
        return ruleBuilder
            .GreaterThan(0)
            .WithMessage($"{fieldName} must be greater than 0");
    }

    /// <summary>
    /// Non-negative decimal validation
    /// </summary>
    public static IRuleBuilderOptions<T, decimal> NonNegativeDecimalValidation<T>(
        this IRuleBuilder<T, decimal> ruleBuilder,
        string fieldName = "Amount")
    {
        return ruleBuilder
            .GreaterThanOrEqualTo(0)
            .WithMessage($"{fieldName} must be greater than or equal to 0");
    }

    /// <summary>
    /// URL validation
    /// </summary>
    public static IRuleBuilderOptions<T, string?> UrlValidation<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        bool isRequired = false)
    {
        var rule = ruleBuilder
            .Matches(@"^https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$")
            .WithMessage("Invalid URL format");

        if (isRequired)
        {
            rule.NotEmpty().WithMessage("URL is required");
        }

        return rule;
    }

    /// <summary>
    /// Employee code validation
    /// </summary>
    public static IRuleBuilderOptions<T, string> EmployeeCodeValidation<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty().WithMessage("Employee code is required")
            .Matches(@"^[A-Z]{2,3}-\d{4,6}$")
            .WithMessage("Employee code must follow format: XX-NNNN or XXX-NNNNNN");
    }

    /// <summary>
    /// Identity number validation (CCCD/CMND Vietnam)
    /// </summary>
    public static IRuleBuilderOptions<T, string> IdentityNumberValidation<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty().WithMessage("Identity number is required")
            .Matches(@"^(\d{9}|\d{12})$")
            .WithMessage("Identity number must be 9 or 12 digits");
    }

    /// <summary>
    /// Working hours validation (0-168 hours per week)
    /// </summary>
    public static IRuleBuilderOptions<T, int> WorkingHoursValidation<T>(
        this IRuleBuilder<T, int> ruleBuilder,
        string fieldName = "Working hours")
    {
        return ruleBuilder
            .InclusiveBetween(0, 168)
            .WithMessage($"{fieldName} must be between 0 and 168 hours per week");
    }
}
