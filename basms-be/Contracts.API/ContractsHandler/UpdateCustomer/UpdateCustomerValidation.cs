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
        // PERSONAL INFO
        // ================================================================
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
    }
}
