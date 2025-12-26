namespace Incidents.API.IncidentHandler.ResponseIncident;

public class ResponseIncidentValidator : AbstractValidator<ResponseIncidentCommand>
{
    public ResponseIncidentValidator()
    {
        RuleFor(x => x.IncidentId)
            .NotEmpty()
            .WithMessage("Incident ID is required");

        RuleFor(x => x.ResponderId)
            .NotEmpty()
            .WithMessage("Responder ID is required");

        RuleFor(x => x.ResponseContent)
            .NotEmpty()
            .WithMessage("Response content is required")
            .MaximumLength(5000)
            .WithMessage("Response content must not exceed 5000 characters");
    }
}
