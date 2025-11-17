namespace Contracts.API.ContractsHandler.CheckContractSignatures;

/// <summary>
/// Command để kiểm tra chữ ký điện tử trong hợp đồng
/// </summary>
public record CheckContractSignaturesCommand(
    Guid DocumentId
) : ICommand<CheckContractSignaturesResult>;

/// <summary>
/// Result của việc kiểm tra chữ ký
/// </summary>
public record CheckContractSignaturesResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int SignatureCount { get; init; }
    public List<string>? Signatures { get; init; }
    public bool HasEmployeeSignature { get; init; }  // Có chữ ký Bên B (Người lao động)
}

internal class CheckContractSignaturesHandler(
    IDbConnectionFactory connectionFactory,
    IS3Service s3Service,
    IDigitalSignatureService signatureService,
    ILogger<CheckContractSignaturesHandler> logger)
    : ICommandHandler<CheckContractSignaturesCommand, CheckContractSignaturesResult>
{
    public async Task<CheckContractSignaturesResult> Handle(
        CheckContractSignaturesCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Checking signatures for DocumentId: {DocumentId}", request.DocumentId);

            // ================================================================
            // LẤY THÔNG TIN DOCUMENT
            // ================================================================

            using var connection = await connectionFactory.CreateConnectionAsync();

            var document = await connection.QueryFirstOrDefaultAsync<Models.ContractDocument>(
                "SELECT * FROM contract_documents WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.DocumentId });

            if (document == null)
            {
                return new CheckContractSignaturesResult
                {
                    Success = false,
                    ErrorMessage = $"Document with ID {request.DocumentId} not found or has been deleted"
                };
            }

            logger.LogInformation("Found document: {DocumentName}", document.DocumentName);

            // ================================================================
            // DOWNLOAD FILE TỪ S3
            // ================================================================

            var (downloadSuccess, fileStream, downloadError) = await s3Service.DownloadFileAsync(
                document.FileUrl,
                cancellationToken);

            if (!downloadSuccess || fileStream == null)
            {
                return new CheckContractSignaturesResult
                {
                    Success = false,
                    ErrorMessage = downloadError ?? "Failed to download file from S3"
                };
            }

            // ================================================================
            // KIỂM TRA CHỮ KÝ
            // ================================================================

            var (verifySuccess, signatures, verifyError) = await signatureService.VerifySignaturesAsync(
                fileStream,
                cancellationToken);

            if (!verifySuccess)
            {
                return new CheckContractSignaturesResult
                {
                    Success = false,
                    ErrorMessage = verifyError ?? "Failed to verify signatures"
                };
            }

            // Đếm số chữ ký
            var signatureCount = signatures?.Count ?? 0;

            // Kiểm tra có ít nhất 1 chữ ký không (Bên B - Người lao động)
            var hasEmployeeSignature = signatureCount >= 1;

            logger.LogInformation(
                "✓ Signature check completed: {Count} signatures found, Employee signed: {HasSignature}",
                signatureCount,
                hasEmployeeSignature);

            return new CheckContractSignaturesResult
            {
                Success = true,
                SignatureCount = signatureCount,
                Signatures = signatures,
                HasEmployeeSignature = hasEmployeeSignature
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check contract signatures");
            return new CheckContractSignaturesResult
            {
                Success = false,
                ErrorMessage = $"Signature check failed: {ex.Message}"
            };
        }
    }
}
