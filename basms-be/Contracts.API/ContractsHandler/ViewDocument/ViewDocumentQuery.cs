namespace Contracts.API.ContractsHandler.ViewDocument;

/// <summary>
/// Query để lấy document chỉ bằng documentId (không cần token)
/// Dùng cho internal access hoặc sau khi đã authenticated
/// </summary>
public record ViewDocumentQuery(
    Guid DocumentId
) : IQuery<ViewDocumentResult>;

/// <summary>
/// Result chứa pre-signed URL và metadata
/// </summary>
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
    public DateTime? UrlExpiresAt { get; init; }
}
