namespace Contracts.API.Extensions;


public interface IDigitalSignatureService
{

    Task<(bool Success, Stream? SignedStream, string? ErrorMessage)> SignWordDocumentAsync(
        Stream documentStream,
        string certificatePath,
        string certificatePassword,
        CancellationToken cancellationToken = default);


    Task<(bool Success, List<string> Signatures, string? ErrorMessage)> VerifySignaturesAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default);
    
    Task<(bool Success, int Count, string? ErrorMessage)> CountSignaturesAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default);
}
