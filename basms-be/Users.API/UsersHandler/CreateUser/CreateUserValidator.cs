namespace Users.API.UsersHandler.CreateUser;


public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.IdentityNumber)
            .NotEmpty().WithMessage("Identity number is required")
            .Length(12).WithMessage("Identity number must be 12 characters");


        RuleFor(x => x.Email)
            .EmailValidation(isRequired: true);


        RuleFor(x => x.Password)
            .PasswordValidation(minLength: 6, maxLength: 100);
        
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required")
            .MaximumLength(255).WithMessage("Full name must not exceed 255 characters");
        
        RuleFor(x => x.Phone)
            .PhoneValidation();
        RuleFor(x => x.BirthDay)
            .BirthDayValidation();
        RuleFor(x => x.BirthMonth)
            .BirthMonthValidation();
        RuleFor(x => x.BirthYear)
            .BirthYearValidation();
        RuleFor(x => x.AuthProvider)
            .AuthProviderValidation();
    }
}
