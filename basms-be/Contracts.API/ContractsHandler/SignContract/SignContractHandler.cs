using Contracts.API.Extensions;

namespace Contracts.API.ContractsHandler.SignContract;

/// <summary>
/// Command để ký điện tử hợp đồng Word
/// </summary>
public record SignContractCommand(
    Stream DocumentStream,
    string CertificatePath,
    string CertificatePassword,
    string OutputFileName
) : ICommand<SignContractResult>;

/// <summary>
/// Result của việc ký hợp đồng
/// </summary>
public record SignContractResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Stream? SignedDocumentStream { get; init; }
    public string? FileName { get; init; }
}

internal class SignContractHandler(
    IDigitalSignatureService signatureService,
    ILogger<SignContractHandler> logger)
    : ICommandHandler<SignContractCommand, SignContractResult>
{
    public async Task<SignContractResult> Handle(
        SignContractCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Signing contract document: {FileName} with certificate: {CertPath}",
                request.OutputFileName,
                request.CertificatePath);

            // Ký document
            var (success, signedStream, error) = await signatureService.SignWordDocumentAsync(
                request.DocumentStream,
                request.CertificatePath,
                request.CertificatePassword,
                cancellationToken);

            if (!success || signedStream == null)
            {
                return new SignContractResult
                {
                    Success = false,
                    ErrorMessage = error ?? "Failed to sign contract document"
                };
            }

            logger.LogInformation("✓ Successfully signed contract document");

            return new SignContractResult
            {
                Success = true,
                SignedDocumentStream = signedStream,
                FileName = request.OutputFileName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sign contract document");
            return new SignContractResult
            {
                Success = false,
                ErrorMessage = $"Sign contract failed: {ex.Message}"
            };
        }
    }
}
