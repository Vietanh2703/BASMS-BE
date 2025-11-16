namespace Contracts.API.Extensions;

/// <summary>
/// Service để ký điện tử Word document sử dụng System.IO.Packaging
/// </summary>
public interface IDigitalSignatureService
{
    /// <summary>
    /// Ký điện tử Word document (.docx)
    /// </summary>
    Task<(bool Success, Stream? SignedStream, string? ErrorMessage)> SignWordDocumentAsync(
        Stream documentStream,
        string certificatePath,
        string certificatePassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra chữ ký điện tử trong Word document
    /// </summary>
    Task<(bool Success, List<string> Signatures, string? ErrorMessage)> VerifySignaturesAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đếm số chữ ký trong document
    /// </summary>
    Task<(bool Success, int Count, string? ErrorMessage)> CountSignaturesAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default);
}
