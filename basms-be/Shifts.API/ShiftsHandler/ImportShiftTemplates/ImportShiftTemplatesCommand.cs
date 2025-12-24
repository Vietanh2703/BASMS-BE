namespace Shifts.API.ShiftsHandler.ImportShiftTemplates;


public record ImportShiftTemplatesCommand(
    Guid ContractId,
    string ContractNumber,
    List<ContractShiftScheduleDto> ShiftSchedules,
    List<ContractLocationDto> Locations,
    Guid? ManagerId = null,
    Guid? ImportedBy = null
) : ICommand<ImportShiftTemplatesResult>;

public record ImportShiftTemplatesResult
{
    public bool Success { get; init; }
    public int TemplatesCreatedCount { get; init; }
    public int TemplatesUpdatedCount { get; init; }
    public int TemplatesSkippedCount { get; init; }
    public List<Guid> CreatedTemplateIds { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public List<TemplateImportInfo> ImportDetails { get; init; } = new();
}

public record TemplateImportInfo
{
    public Guid TemplateId { get; init; }
    public string TemplateCode { get; init; } = string.Empty;
    public string TemplateName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty; // "Created" | "Updated" | "Skipped"
    public string? Reason { get; init; }
    public TimeValidationResult TimeValidation { get; init; } = new();
}

public record TimeValidationResult
{
    public bool IsValid { get; init; }
    public bool CrossesMidnight { get; init; }
    public decimal ActualDurationHours { get; init; }
    public decimal DeclaredDurationHours { get; init; }
    public bool DurationMatches { get; init; }
    public bool IsNightShift { get; init; }
    public List<string> Warnings { get; init; } = new();
    public List<string> Errors { get; init; } = new();
}