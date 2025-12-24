namespace Contracts.API.Models;

[Table("contract_documents")]
public class ContractDocument
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string Version { get; set; } = "1.0";
    public string? Tokens { get; set; }
    public DateTime? TokenExpiredDay { get; set; }
    public DateTime? DocumentDate { get; set; }
    public Guid? UploadedBy { get; set; }
    public string? DocumentEmail { get; set; }
    public string? DocumentCustomerName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? SignDate { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
