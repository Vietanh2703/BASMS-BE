using BuildingBlocks.CQRS;

namespace Contracts.API.ContractsHandler.ApproveContractDocument;

/// <summary>
/// Command để approve contract document
/// Workflow: 2 bên ký xong → Director review → Approve → Document type = approved_document, version = completed
/// </summary>
public record ApproveContractDocumentCommand(
    Guid DocumentId,
    Guid? ApprovedBy = null,
    string? Notes = null
) : ICommand<ApproveContractDocumentResult>;

/// <summary>
/// Result sau khi approve contract document
/// </summary>
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
