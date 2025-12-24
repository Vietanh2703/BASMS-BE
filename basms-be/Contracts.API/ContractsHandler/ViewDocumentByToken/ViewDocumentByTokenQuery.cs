namespace Contracts.API.ContractsHandler.ViewDocumentByToken;

public record ViewDocumentByTokenQuery(
    Guid DocumentId,
    string Token
) : IQuery<ViewDocumentByTokenResult>;

public record ViewDocumentByTokenResult
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
