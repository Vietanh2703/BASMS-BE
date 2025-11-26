namespace Contracts.API.ContractsHandler.SignContract;

/// <summary>
/// Command để chèn ảnh chữ ký vào hợp đồng
/// </summary>
public record SignContractFromDocumentCommand(
    Guid DocumentId,  // FilledDocumentId from S3
    IFormFile SignatureImage  // Signature image to insert into content control
) : ICommand<SignContractFromDocumentResult>;

/// <summary>
/// Result của việc chèn chữ ký vào hợp đồng
/// </summary>
public record SignContractFromDocumentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? DocumentId { get; init; }
    public string? FileUrl { get; init; }
    public string? FileName { get; init; }
}

internal class SignContractFromDocumentHandler(
    IDbConnectionFactory connectionFactory,
    IS3Service s3Service,
    IWordContractService wordContractService,
    ILogger<SignContractFromDocumentHandler> logger)
    : ICommandHandler<SignContractFromDocumentCommand, SignContractFromDocumentResult>
{
    public async Task<SignContractFromDocumentResult> Handle(
        SignContractFromDocumentCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Inserting signature image for DocumentId: {DocumentId}", request.DocumentId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: LẤY THÔNG TIN DOCUMENT
            // ================================================================
            var document = await connection.QueryFirstOrDefaultAsync<ContractDocument>(
                "SELECT * FROM contract_documents WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.DocumentId });

            if (document == null)
            {
                return new SignContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = $"Document {request.DocumentId} not found"
                };
            }

            logger.LogInformation("Found document: {DocumentName}", document.DocumentName);

            // ================================================================
            // BƯỚC 2: DOWNLOAD FILE TỪ S3
            // ================================================================
            var (downloadSuccess, fileStream, downloadError) = await s3Service.DownloadFileAsync(
                document.FileUrl,
                cancellationToken);

            if (!downloadSuccess || fileStream == null)
            {
                return new SignContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = downloadError ?? "Failed to download document from S3"
                };
            }

            logger.LogInformation("Downloaded document from S3: {FileUrl}", document.FileUrl);

            // ================================================================
            // BƯỚC 3: INSERT SIGNATURE IMAGE
            // ================================================================
            logger.LogInformation("Inserting signature image into content control 'DigitalSignature'");

            Stream documentWithSignature;
            try
            {
                using var signatureImageStream = request.SignatureImage.OpenReadStream();

                var (insertSuccess, modifiedStream, insertError) =
                    await wordContractService.InsertSignatureImageAsync(
                        fileStream,
                        "DigitalSignature",
                        signatureImageStream,
                        request.SignatureImage.FileName,
                        cancellationToken);

                fileStream.Dispose();

                if (!insertSuccess || modifiedStream == null)
                {
                    return new SignContractFromDocumentResult
                    {
                        Success = false,
                        ErrorMessage = insertError ?? "Failed to insert signature image into document"
                    };
                }

                documentWithSignature = modifiedStream;
                logger.LogInformation("✓ Signature image inserted successfully");
            }
            catch (Exception ex)
            {
                fileStream.Dispose();
                logger.LogError(ex, "Error inserting signature image");
                return new SignContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to insert signature image: {ex.Message}"
                };
            }

            // ================================================================
            // BƯỚC 4: UPLOAD FILE BACK TO S3 (OVERWRITE)
            // ================================================================
            var (uploadSuccess, fileUrl, uploadError) = await s3Service.UploadFileWithCustomKeyAsync(
                documentWithSignature,
                document.FileUrl,  // Use same S3 key to overwrite
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                cancellationToken);

            documentWithSignature.Dispose();

            if (!uploadSuccess || string.IsNullOrEmpty(fileUrl))
            {
                return new SignContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = uploadError ?? "Failed to upload document to S3"
                };
            }

            logger.LogInformation("Uploaded document with signature to S3: {FileUrl}", fileUrl);

            return new SignContractFromDocumentResult
            {
                Success = true,
                DocumentId = document.Id,
                FileUrl = fileUrl,
                FileName = document.DocumentName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inserting signature into document");
            return new SignContractFromDocumentResult
            {
                Success = false,
                ErrorMessage = $"Insert signature failed: {ex.Message}"
            };
        }
    }

}