namespace Incidents.API.IncidentHandler.CreateIncident;

public class CreateIncidentValidator : AbstractValidator<CreateIncidentCommand>
{
    public CreateIncidentValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(255).WithMessage("Title cannot exceed 255 characters");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required");

        RuleFor(x => x.IncidentType)
            .NotEmpty().WithMessage("IncidentType is required")
            .Must(BeValidIncidentType).WithMessage("Invalid IncidentType");

        RuleFor(x => x.Severity)
            .NotEmpty().WithMessage("Severity is required")
            .Must(BeValidSeverity).WithMessage("Invalid Severity");

        RuleFor(x => x.IncidentTime)
            .NotEmpty().WithMessage("IncidentTime is required")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("IncidentTime cannot be in the future");

        RuleFor(x => x.Location)
            .NotEmpty().WithMessage("Location is required");

        RuleFor(x => x.ReporterId)
            .NotEmpty().WithMessage("ReporterId is required");

        RuleFor(x => x.ReporterName)
            .NotEmpty().WithMessage("ReporterName is required");

        RuleFor(x => x.ReporterEmail)
            .NotEmpty().WithMessage("ReporterEmail is required")
            .EmailAddress().WithMessage("Invalid email format");
    }

    private bool BeValidIncidentType(string type)
    {
        var validTypes = new[] { "INTRUSION", "THEFT", "FIRE", "MEDICAL", "EQUIPMENT_FAILURE", "VANDALISM", "DISPUTE", "OTHER" };
        return validTypes.Contains(type.ToUpper());
    }

    private bool BeValidSeverity(string severity)
    {
        var validSeverities = new[] { "LOW", "MEDIUM", "HIGH", "CRITICAL" };
        return validSeverities.Contains(severity.ToUpper());
    }
}
