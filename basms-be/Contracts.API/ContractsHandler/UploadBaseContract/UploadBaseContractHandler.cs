namespace Contracts.API.ContractsHandler.UploadBaseContract;

public record UploadBaseContractCommand(
    IFormFile File,
    string? Category,
    string? FolderPath = null 
) : ICommand<UploadBaseContractResult>;


public record UploadBaseContractResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? DocumentId { get; init; }
    public string? FileUrl { get; init; }
    public string? FileName { get; init; }
    public long FileSize { get; init; }
    public string? Category { get; init; }
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
            if (request.File == null || request.File.Length == 0)
            {
                return new UploadBaseContractResult
                {
                    Success = false,
                    ErrorMessage = "No file uploaded"
                };
            }
            
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

            var folderPath = request.FolderPath ?? "templates";
            
            if (fileName.Contains("LAO_DONG", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("WORKING", StringComparison.OrdinalIgnoreCase))
            {
                folderPath = "templates/working";
            }
            else if (fileName.Contains("DICH-VU", StringComparison.OrdinalIgnoreCase) ||
                     fileName.Contains("SERVICE", StringComparison.OrdinalIgnoreCase))
            {
                folderPath = "templates/service";
            }

            logger.LogInformation("Target folder: {FolderPath}", folderPath);


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
            
            var documentId = Guid.NewGuid();
            using var connection = await connectionFactory.CreateConnectionAsync();

            var document = new ContractDocument
            {
                Id = documentId,
                Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category,
                DocumentName = fileName,
                FileUrl = s3Key, 
                FileSize = fileSize,
                DocumentType = "template", 
                Version = "base",
                UploadedBy = Guid.Empty, 
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
                Category = document.Category,
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