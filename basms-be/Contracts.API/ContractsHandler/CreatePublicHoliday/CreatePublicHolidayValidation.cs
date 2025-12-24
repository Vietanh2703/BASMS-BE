namespace Contracts.API.ContractsHandler.CreatePublicHoliday;

public class CreatePublicHolidayValidation : AbstractValidator<CreatePublicHolidayCommand>
{
    public CreatePublicHolidayValidation()
    {
        RuleFor(x => x.HolidayDate)
            .NotEmpty()
            .WithMessage("Holiday date is required");
        RuleFor(x => x.HolidayName)
            .NotEmpty()
            .WithMessage("Holiday name is required")
            .MaximumLength(200)
            .WithMessage("Holiday name must not exceed 200 characters");
        RuleFor(x => x.HolidayNameEn)
            .MaximumLength(200)
            .WithMessage("Holiday name (English) must not exceed 200 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.HolidayNameEn));
        RuleFor(x => x.HolidayCategory)
            .NotEmpty()
            .WithMessage("Holiday category is required")
            .Must(category => new[] { "national", "tet", "regional", "substitute" }
                .Contains(category.ToLower()))
            .WithMessage("Holiday category must be one of: national, tet, regional, substitute");
        RuleFor(x => x.TetDayNumber)
            .InclusiveBetween(1, 10)
            .WithMessage("Tet day number must be between 1 and 10")
            .When(x => x.TetDayNumber.HasValue);

        RuleFor(x => x.HolidayStartDate)
            .LessThanOrEqualTo(x => x.HolidayEndDate)
            .WithMessage("Holiday start date must be before or equal to end date")
            .When(x => x.HolidayStartDate.HasValue && x.HolidayEndDate.HasValue);

        RuleFor(x => x.TotalHolidayDays)
            .GreaterThan(0)
            .WithMessage("Total holiday days must be greater than 0")
            .LessThanOrEqualTo(30)
            .WithMessage("Total holiday days must not exceed 30 days")
            .When(x => x.TotalHolidayDays.HasValue);
        RuleFor(x => x.Year)
            .GreaterThanOrEqualTo(2020)
            .WithMessage("Year must be 2020 or later")
            .LessThanOrEqualTo(2100)
            .WithMessage("Year must be 2100 or earlier");
        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.AppliesToRegions)
            .MaximumLength(500)
            .WithMessage("Applies to regions must not exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.AppliesToRegions));
        RuleFor(x => x)
            .Must(x => !x.IsTetHoliday || x.IsTetPeriod)
            .WithMessage("If IsTetHoliday is true, IsTetPeriod must also be true");

        RuleFor(x => x)
            .Must(x => !x.IsTetPeriod || x.HolidayStartDate.HasValue)
            .WithMessage("If IsTetPeriod is true, HolidayStartDate is required");

        RuleFor(x => x)
            .Must(x => !x.IsTetPeriod || x.HolidayEndDate.HasValue)
            .WithMessage("If IsTetPeriod is true, HolidayEndDate is required");
    }
}
