namespace Contracts.API.ContractsHandler.UploadBaseContract;

/// <summary>
/// Command để upload file template mẫu lên S3
/// </summary>
public record UploadBaseContractCommand(
    IFormFile File,
    string? FolderPath = null  // Optional: "templates/service" hoặc "templates/working"
) : ICommand<UploadBaseContractResult>;

/// <summary>
/// Result của việc upload template
/// </summary>
public record UploadBaseContractResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? DocumentId { get; init; }
    public string? FileUrl { get; init; }
    public string? FileName { get; init; }
    public long FileSize { get; init; }
}

internal class UploadBaseContractHandler(
    IDbConnectionFactory connectionFactory,
    IS3Service s3Service,
    ILogger<UploadBaseContractHandler> logger)
    : ICommandHandler<UploadBaseContractCommand, UploadBaseContractResult>
{
    public async Task<UploadBaseContractResult> Handle(
        UploadBaseContractCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ================================================================
            // BƯỚC 1: VALIDATE FILE
            // ================================================================
            if (request.File == null || request.File.Length == 0)
            {
                return new UploadBaseContractResult
                {
                    Success = false,
                    ErrorMessage = "No file uploaded"
                };
            }

            // Validate file extension
            var allowedExtensions = new[] { ".docx", ".pdf" };
            var fileExtension = Path.GetExtension(request.File.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return new UploadBaseContractResult
                {
                    Success = false,
                    ErrorMessage = $"Invalid file type. Only {string.Join(", ", allowedExtensions)} are allowed"
                };
            }

            var fileName = request.File.FileName;
            var fileSize = request.File.Length;

            logger.LogInformation(
                "Uploading base contract template: {FileName} ({FileSize} bytes)",
                fileName,
                fileSize);

            // ================================================================
            // BƯỚC 2: XÁC ĐỊNH FOLDER PATH
            // ================================================================
            var folderPath = request.FolderPath ?? "templates";

            // Nếu file name chứa "LAO-DONG" hoặc "WORKING" -> working contract
            if (fileName.Contains("LAO-DONG", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("WORKING", StringComparison.OrdinalIgnoreCase))
            {
                folderPath = "templates/working";
            }
            // Nếu file name chứa "DICH-VU" hoặc "SERVICE" -> service contract
            else if (fileName.Contains("DICH-VU", StringComparison.OrdinalIgnoreCase) ||
                     fileName.Contains("SERVICE", StringComparison.OrdinalIgnoreCase))
            {
                folderPath = "templates/service";
            }

            logger.LogInformation("Target folder: {FolderPath}", folderPath);

            // ================================================================
            // BƯỚC 3: UPLOAD FILE LÊN S3 (GIỮ NGUYÊN TÊN FILE)
            // ================================================================
            var s3Key = $"{folderPath}/{fileName}";

            using var fileStream = request.File.OpenReadStream();

            var (uploadSuccess, s3Url, uploadError) = await s3Service.UploadFileWithCustomKeyAsync(
                fileStream,
                s3Key,
                request.File.ContentType,
                cancellationToken);

            if (!uploadSuccess || string.IsNullOrEmpty(s3Url))
            {
                return new UploadBaseContractResult
                {
                    Success = false,
                    ErrorMessage = uploadError ?? "Failed to upload file to S3"
                };
            }

            logger.LogInformation("Uploaded template to S3: {S3Url}", s3Url);

            // ================================================================
            // BƯỚC 4: TẠO RECORD TRONG CONTRACT_DOCUMENTS
            // ================================================================
            var documentId = Guid.NewGuid();
            using var connection = await connectionFactory.CreateConnectionAsync();

            var document = new ContractDocument
            {
                Id = documentId,
                DocumentName = fileName,
                FileUrl = s3Key,  // LƯU S3 KEY THAY VÌ FULL URL để tránh encoding issues
                FileSize = fileSize,
                DocumentType = "template", // Loại: template mẫu
                Version = "base", // Version: base template
                UploadedBy = Guid.Empty, // No user context for system upload
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            await connection.InsertAsync(document);
            logger.LogInformation("Created ContractDocument record: {DocumentId} with S3 key: {S3Key}",
                documentId, s3Key);

            return new UploadBaseContractResult
            {
                Success = true,
                DocumentId = documentId,
                FileUrl = s3Url,
                FileName = fileName,
                FileSize = fileSize
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading base contract template");
            return new UploadBaseContractResult
            {
                Success = false,
                ErrorMessage = $"Upload failed: {ex.Message}"
            };
        }
    }
}