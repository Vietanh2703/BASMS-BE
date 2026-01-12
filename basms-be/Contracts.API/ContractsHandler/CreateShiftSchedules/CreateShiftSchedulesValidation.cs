namespace Contracts.API.ContractsHandler.CreateShiftSchedules;

public class CreateShiftSchedulesValidation : AbstractValidator<CreateShiftSchedulesCommand>
{
    public CreateShiftSchedulesValidation()
    {
        RuleFor(x => x.ContractId)
            .NotEmpty()
            .WithMessage("Contract ID is required");

        RuleFor(x => x.ScheduleName)
            .NotEmpty()
            .WithMessage("Schedule name is required")
            .MaximumLength(200)
            .WithMessage("Schedule name must not exceed 200 characters");

        RuleFor(x => x.ScheduleType)
            .NotEmpty()
            .WithMessage("Schedule type is required")
            .Must(type => new[] { "regular", "overtime", "standby", "emergency", "event" }.Contains(type))
            .WithMessage("Schedule type must be one of: regular, overtime, standby, emergency, event");

        RuleFor(x => x.ShiftStartTime)
            .Must(time => time >= TimeSpan.Zero && time < TimeSpan.FromHours(24))
            .WithMessage("Shift start time must be between 00:00:00 and 23:59:59");

        RuleFor(x => x.ShiftEndTime)
            .Must(time => time >= TimeSpan.Zero && time < TimeSpan.FromHours(24))
            .WithMessage("Shift end time must be between 00:00:00 and 23:59:59");

        RuleFor(x => x.DurationHours)
            .GreaterThan(0)
            .WithMessage("Duration hours must be greater than 0")
            .LessThanOrEqualTo(24)
            .WithMessage("Duration hours must not exceed 24 hours");

        RuleFor(x => x.BreakMinutes)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Break minutes cannot be negative")
            .LessThanOrEqualTo(480)
            .WithMessage("Break minutes must not exceed 480 minutes (8 hours)");
        
        RuleFor(x => x.GuardsPerShift)
            .GreaterThan(0)
            .WithMessage("Guards per shift must be at least 1")
            .LessThanOrEqualTo(100)
            .WithMessage("Guards per shift must not exceed 100");
        
        RuleFor(x => x.RecurrenceType)
            .NotEmpty()
            .WithMessage("Recurrence type is required")
            .Must(type => new[] { "daily", "weekly", "bi_weekly", "monthly", "specific_dates" }.Contains(type))
            .WithMessage("Recurrence type must be one of: daily, weekly, bi_weekly, monthly, specific_dates");

        RuleFor(x => x)
            .Must(x => x.RecurrenceType != "weekly" ||
                      (x.AppliesMonday || x.AppliesTuesday || x.AppliesWednesday ||
                       x.AppliesThursday || x.AppliesFriday || x.AppliesSaturday || x.AppliesSunday))
            .WithMessage("For weekly recurrence, at least one day of the week must be selected");

        RuleFor(x => x.MonthlyDates)
            .Must(BeValidMonthlyDates)
            .WithMessage("Monthly dates must be comma-separated numbers between 1-31 (e.g., '1,15,30')")
            .When(x => x.RecurrenceType == "monthly" && !string.IsNullOrWhiteSpace(x.MonthlyDates));
        
        RuleFor(x => x.MinimumExperienceMonths)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Minimum experience months cannot be negative")
            .LessThanOrEqualTo(600)
            .WithMessage("Minimum experience months must not exceed 600 (50 years)");
        
        RuleFor(x => x.GenerateAdvanceDays)
            .GreaterThan(0)
            .WithMessage("Generate advance days must be at least 1")
            .LessThanOrEqualTo(365)
            .WithMessage("Generate advance days must not exceed 365");

        RuleFor(x => x.EffectiveFrom)
            .NotEmpty()
            .WithMessage("Effective from date is required")
            .Must(BeAfterToday)
            .WithMessage(x => $"EffectiveFrom {x.EffectiveFrom:yyyy-MM-dd} phải sau ngày hôm nay ({DateTime.UtcNow.Date:yyyy-MM-dd})");

        RuleFor(x => x)
            .Must(x => !x.EffectiveTo.HasValue || x.EffectiveTo.Value >= x.EffectiveFrom)
            .WithMessage("Effective to date must be on or after effective from date")
            .When(x => x.EffectiveTo.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("Notes must not exceed 1000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }

    private bool BeValidMonthlyDates(string? monthlyDates)
    {
        if (string.IsNullOrWhiteSpace(monthlyDates))
            return true;

        var dates = monthlyDates.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var date in dates)
        {
            if (!int.TryParse(date.Trim(), out int day) || day < 1 || day > 31)
                return false;
        }

        return true;
    }

    private bool BeAfterToday(DateTime date)
    {
        var today = DateTime.UtcNow.Date;
        return date.Date > today;
    }
}
