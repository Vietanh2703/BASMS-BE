namespace Incidents.API.Models;

[Table("incidents")]
public class Incidents
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public string IncidentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime IncidentTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? ShiftLocation { get; set; }
    public Guid? ShiftId { get; set; }
    public Guid? ShiftAssignmentId { get; set; }
    public Guid ReporterId { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public string ReporterEmail { get; set; } = string.Empty;
    public DateTime ReportedTime { get; set; }
    public string Status { get; set; } = "REPORTED";
    public string? ResponseContent { get; set; }
    public Guid? ResponderId { get; set; }
    public string? ResponderName { get; set; }
    public string? ResponderEmail { get; set; }
    public DateTime? RespondedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    [Write(false)]
    [Computed]
    public virtual ICollection<IncidentMedia> Media { get; set; } = new List<IncidentMedia>();
}
