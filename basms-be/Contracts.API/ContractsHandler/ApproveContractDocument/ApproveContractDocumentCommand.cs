namespace Contracts.API.ContractsHandler.ApproveContractDocument;

public record ApproveContractDocumentCommand(
    Guid DocumentId,
    Guid? ApprovedBy = null,
    string? Notes = null
) : ICommand<ApproveContractDocumentResult>;

public record ApproveContractDocumentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? DocumentId { get; init; }
    public string? DocumentName { get; init; }
    public string? DocumentType { get; init; }
    public string? Version { get; init; }
    public DateTime? ApprovedAt { get; init; }
}
