namespace Incidents.API.Models;

[Table("incident_media")]
public class IncidentMedia
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Caption { get; set; }
    public int? DisplayOrder { get; set; }
    public Guid? UploadedBy { get; set; }
    public string? UploadedByName { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    [Write(false)]
    [Computed]
    public virtual Incidents? Incident { get; set; }
}
