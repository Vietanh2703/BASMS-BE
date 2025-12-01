namespace Contracts.API.ContractsHandler.UpdateCustomer;


public class UpdateCustomerValidation : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerValidation()
    {
        // ================================================================
        // CUSTOMER ID
        // ================================================================
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required");

        // ================================================================
        // COMPANY INFO
        // ================================================================
        RuleFor(x => x.CompanyName)
            .NotEmpty()
            .WithMessage("Company name is required")
            .MaximumLength(200)
            .WithMessage("Company name must not exceed 200 characters");

        // ================================================================
        // CONTACT PERSON
        // ================================================================
        RuleFor(x => x.ContactPersonName)
            .NotEmpty()
            .WithMessage("Contact person name is required")
            .MaximumLength(100)
            .WithMessage("Contact person name must not exceed 100 characters");

        RuleFor(x => x.ContactPersonTitle)
            .MaximumLength(100)
            .WithMessage("Contact person title must not exceed 100 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPersonTitle));

        // ================================================================
        // IDENTITY NUMBER (CCCD)
        // ================================================================
        RuleFor(x => x.IdentityNumber)
            .NotEmpty()
            .WithMessage("Identity number (CCCD) is required")
            .Matches(@"^\d{9}$|^\d{12}$")
            .WithMessage("Identity number must be 9 or 12 digits");

        RuleFor(x => x.IdentityIssueDate)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("Identity issue date cannot be in the future")
            .When(x => x.IdentityIssueDate.HasValue);

        RuleFor(x => x.IdentityIssuePlace)
            .MaximumLength(200)
            .WithMessage("Identity issue place must not exceed 200 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.IdentityIssuePlace));

        // ================================================================
        // CONTACT INFO
        // ================================================================
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Invalid email format")
            .MaximumLength(100)
            .WithMessage("Email must not exceed 100 characters");

        RuleFor(x => x.Phone)
            .NotEmpty()
            .WithMessage("Phone number is required")
            .Matches(@"^(\+84|0)[0-9]{9,10}$")
            .WithMessage("Invalid Vietnam phone number format (e.g., +84329465423 or 0329465423)");

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(500)
            .WithMessage("Avatar URL must not exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.AvatarUrl));

        // ================================================================
        // PERSONAL INFO
        // ================================================================
        RuleFor(x => x.Gender)
            .Must(gender => gender == null || gender == "male" || gender == "female" || gender == "other")
            .WithMessage("Gender must be 'male', 'female', or 'other'")
            .When(x => !string.IsNullOrWhiteSpace(x.Gender));

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateTime.UtcNow)
            .WithMessage("Date of birth must be in the past")
            .GreaterThan(DateTime.UtcNow.AddYears(-150))
            .WithMessage("Date of birth is too far in the past");

        // ================================================================
        // ADDRESS
        // ================================================================
        RuleFor(x => x.Address)
            .NotEmpty()
            .WithMessage("Address is required")
            .MaximumLength(500)
            .WithMessage("Address must not exceed 500 characters");

        RuleFor(x => x.City)
            .MaximumLength(100)
            .WithMessage("City must not exceed 100 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.City));

        RuleFor(x => x.District)
            .MaximumLength(100)
            .WithMessage("District must not exceed 100 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.District));

        // ================================================================
        // BUSINESS CLASSIFICATION
        // ================================================================
        RuleFor(x => x.Industry)
            .Must(industry => industry == null ||
                  new[] { "retail", "office", "manufacturing", "hospital", "school", "residential" }
                      .Contains(industry.ToLower()))
            .WithMessage("Industry must be one of: retail, office, manufacturing, hospital, school, residential")
            .When(x => !string.IsNullOrWhiteSpace(x.Industry));

        RuleFor(x => x.CompanySize)
            .Must(size => size == null ||
                  new[] { "small", "medium", "large", "enterprise" }
                      .Contains(size.ToLower()))
            .WithMessage("Company size must be one of: small, medium, large, enterprise")
            .When(x => !string.IsNullOrWhiteSpace(x.CompanySize));

        // ================================================================
        // STATUS
        // ================================================================
        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required")
            .Must(status => new[] { "active", "inactive", "suspended", "assigning_manager" }
                .Contains(status.ToLower()))
            .WithMessage("Status must be one of: active, inactive, suspended, assigning_manager");

        // ================================================================
        // NOTES
        // ================================================================
        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("Notes must not exceed 1000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}
