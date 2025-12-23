namespace Incidents.API.IncidentHandler.CreateIncident;

public record MediaFileDto
{
    public Stream FileStream { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string MediaType { get; init; } = "IMAGE";
    public string? Caption { get; init; }
}

public record CreateIncidentCommand(
    string Title,
    string Description,
    string IncidentType,
    string Severity,
    DateTime IncidentTime,
    string Location,
    string? ShiftLocation,
    Guid? ShiftId,
    Guid? ShiftAssignmentId,
    Guid ReporterId,
    string ReporterName,
    string ReporterEmail,
    string? ReporterRole,
    List<MediaFileDto>? MediaFiles
) : ICommand<CreateIncidentResult>;

public record CreateIncidentResult
{
    public Guid IncidentId { get; init; }
    public string IncidentCode { get; init; } = string.Empty;
    public int MediaFilesUploaded { get; init; }
    public List<string> UploadedFileUrls { get; init; } = new();
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
