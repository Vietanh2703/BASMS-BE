namespace Contracts.API.ContractsHandler.ViewDocument;

public record ViewDocumentQuery(
    Guid DocumentId
) : IQuery<ViewDocumentResult>;

public record ViewDocumentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public string? FileUrl { get; init; }
    public string? FileName { get; init; }
    public string? ContentType { get; init; }
    public long? FileSize { get; init; }
    public Guid? DocumentId { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UrlExpiresAt { get; init; }
}
