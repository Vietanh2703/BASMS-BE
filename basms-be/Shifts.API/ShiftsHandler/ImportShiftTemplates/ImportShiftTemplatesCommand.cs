using BuildingBlocks.CQRS;
using BuildingBlocks.Messaging.Events;

namespace Shifts.API.ShiftsHandler.ImportShiftTemplates;

/// <summary>
/// Command để import ShiftTemplates từ ContractActivatedEvent
/// Workflow: Contract activated → Import shift schedules as templates
/// </summary>
public record ImportShiftTemplatesCommand(
    Guid ContractId,
    string ContractNumber,
    List<ContractShiftScheduleDto> ShiftSchedules,
    List<ContractLocationDto> Locations,
    Guid? ManagerId = null,
    Guid? ImportedBy = null
) : ICommand<ImportShiftTemplatesResult>;

/// <summary>
/// Result sau khi import
/// </summary>
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

/// <summary>
/// Chi tiết từng template được import
/// </summary>
public record TemplateImportInfo
{
    public Guid TemplateId { get; init; }
    public string TemplateCode { get; init; } = string.Empty;
    public string TemplateName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty; // "Created" | "Updated" | "Skipped"
    public string? Reason { get; init; }
    public TimeValidationResult TimeValidation { get; init; } = new();
}

/// <summary>
/// Kết quả validation thời gian
/// </summary>
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