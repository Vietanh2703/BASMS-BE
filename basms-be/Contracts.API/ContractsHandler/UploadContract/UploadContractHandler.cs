using Contracts.API.Extensions;

namespace Contracts.API.ContractsHandler.UploadContract;

/// <summary>
/// Command để upload contract document lên S3
/// Chỉ chấp nhận Word/PDF, tối đa 10MB
/// </summary>
public record UploadContractCommand(
    Guid ContractId,
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSize,
    string DocumentType,
    DateTime? DocumentDate,
    Guid UploadedBy
) : ICommand<UploadContractResult>;

/// <summary>
/// Result của việc upload contract
/// </summary>
public record UploadContractResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? DocumentId { get; init; }
    public string? FileUrl { get; init; }
    public string? DocumentName { get; init; }
    public long? FileSize { get; init; }
    public string? DocumentType { get; init; }
}

internal class UploadContractHandler(
    IDbConnectionFactory connectionFactory,
    IS3Service s3Service,
    ILogger<UploadContractHandler> logger)
    : ICommandHandler<UploadContractCommand, UploadContractResult>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
    private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx" };
    private static readonly string[] AllowedContentTypes =
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    public async Task<UploadContractResult> Handle(
        UploadContractCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Uploading contract document for ContractId: {ContractId}, File: {FileName} ({FileSize} bytes)",
                request.ContractId,
                request.FileName,
                request.FileSize);

            // ================================================================
            // VALIDATION
            // ================================================================

            // 0. Validate Contract exists
            using var connection = await connectionFactory.CreateConnectionAsync();

            var contractExists = await connection.QueryFirstOrDefaultAsync<Models.Contract>(
                "SELECT * FROM contracts WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.ContractId });

            if (contractExists == null)
            {
                return new UploadContractResult
                {
                    Success = false,
                    ErrorMessage = $"Contract with ID {request.ContractId} not found or has been deleted"
                };
            }

            logger.LogInformation(
                "Contract verified: {ContractNumber} - {ContractTitle}",
                contractExists.ContractNumber,
                contractExists.ContractTitle);

            // 1. Validate file size (≤ 10MB)
            if (request.FileSize > MaxFileSizeBytes)
            {
                return new UploadContractResult
                {
                    Success = false,
                    ErrorMessage = $"File size exceeds maximum limit of 10MB. Current size: {request.FileSize / 1024.0 / 1024.0:F2}MB"
                };
            }

            // 2. Validate file extension
            var fileExtension = Path.GetExtension(request.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(fileExtension))
            {
                return new UploadContractResult
                {
                    Success = false,
                    ErrorMessage = $"Invalid file type. Only PDF and Word documents (.pdf, .doc, .docx) are allowed. Received: {fileExtension}"
                };
            }

            // 3. Validate content type
            if (!AllowedContentTypes.Contains(request.ContentType.ToLowerInvariant()))
            {
                return new UploadContractResult
                {
                    Success = false,
                    ErrorMessage = $"Invalid content type. Received: {request.ContentType}"
                };
            }

            // 4. Validate document type
            var validDocumentTypes = new[] { "contract", "amendment", "appendix", "requirements", "site_plan" };
            if (!validDocumentTypes.Contains(request.DocumentType.ToLowerInvariant()))
            {
                return new UploadContractResult
                {
                    Success = false,
                    ErrorMessage = $"Invalid document type. Must be one of: {string.Join(", ", validDocumentTypes)}"
                };
            }

            // ================================================================
            // UPLOAD TO S3
            // ================================================================

            var (uploadSuccess, fileUrl, uploadError) = await s3Service.UploadFileAsync(
                request.FileStream,
                request.FileName,
                request.ContentType,
                cancellationToken);

            if (!uploadSuccess || string.IsNullOrEmpty(fileUrl))
            {
                return new UploadContractResult
                {
                    Success = false,
                    ErrorMessage = uploadError ?? "Failed to upload file to S3"
                };
            }

            // ================================================================
            // SAVE TO DATABASE
            // ================================================================

            using var transaction = connection.BeginTransaction();

            try
            {
                var document = new ContractDocument
                {
                    Id = Guid.NewGuid(),
                    ContractId = request.ContractId,
                    DocumentType = request.DocumentType.ToLowerInvariant(),
                    DocumentName = request.FileName,
                    FileUrl = fileUrl,
                    FileSize = request.FileSize,
                    MimeType = request.ContentType,
                    Version = "1.0",
                    DocumentDate = request.DocumentDate ?? DateTime.UtcNow,
                    UploadedBy = request.UploadedBy,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow
                };

                await connection.InsertAsync(document, transaction);

                transaction.Commit();

                logger.LogInformation(
                    "✓ Contract document uploaded successfully: DocumentId={DocumentId}, ContractId={ContractId}, File={FileName}",
                    document.Id,
                    request.ContractId,
                    request.FileName);

                return new UploadContractResult
                {
                    Success = true,
                    DocumentId = document.Id,
                    FileUrl = fileUrl,
                    DocumentName = request.FileName,
                    FileSize = request.FileSize,
                    DocumentType = request.DocumentType
                };
            }
            catch (Exception dbEx)
            {
                transaction.Rollback();

                // Nếu lưu DB thất bại, xóa file trên S3
                logger.LogWarning("Database save failed, attempting to delete uploaded file from S3");
                await s3Service.DeleteFileAsync(fileUrl, cancellationToken);

                logger.LogError(dbEx, "Error saving contract document to database");
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload contract document: {FileName}", request.FileName);
            return new UploadContractResult
            {
                Success = false,
                ErrorMessage = $"Upload failed: {ex.Message}"
            };
        }
    }
}