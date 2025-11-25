namespace Contracts.API.ContractsHandler.ViewDocumentByToken;

/// <summary>
/// Query để lấy document bằng token bảo mật
/// Dùng cho việc xem document trước khi ký điện tử
/// </summary>
public record ViewDocumentByTokenQuery(
    string Token
) : IQuery<ViewDocumentByTokenResult>;

/// <summary>
/// Result chứa file stream và metadata
/// </summary>
public record ViewDocumentByTokenResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public Stream? FileStream { get; init; }
    public string? FileName { get; init; }
    public string? ContentType { get; init; }
    public long? FileSize { get; init; }
    public Guid? DocumentId { get; init; }
}
