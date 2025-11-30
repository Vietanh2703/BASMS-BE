namespace Users.API.UsersHandler.ValidateEmail;

public class ValidateEmailValidator : AbstractValidator<ValidateEmailCommand>
{
    public ValidateEmailValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");
    }
}
