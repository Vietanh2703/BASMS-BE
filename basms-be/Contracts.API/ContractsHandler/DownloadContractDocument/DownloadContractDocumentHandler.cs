namespace Contracts.API.ContractsHandler.DownloadContractDocument;

public record DownloadContractDocumentQuery(Guid DocumentId) : IQuery<DownloadContractDocumentResult>;

public record DownloadContractDocumentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Stream? FileStream { get; init; }
    public string? FileName { get; init; }
    public string? ContentType { get; init; }
    public long? FileSize { get; init; }
}

internal class DownloadContractDocumentHandler(
    IDbConnectionFactory connectionFactory,
    IS3Service s3Service,
    ILogger<DownloadContractDocumentHandler> logger)
    : IQueryHandler<DownloadContractDocumentQuery, DownloadContractDocumentResult>
{
    public async Task<DownloadContractDocumentResult> Handle(
        DownloadContractDocumentQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Downloading contract document: {DocumentId}", request.DocumentId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            var document = await connection.QueryFirstOrDefaultAsync<Models.ContractDocument>(
                "SELECT * FROM contract_documents WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.DocumentId });

            if (document == null)
            {
                logger.LogWarning("Document not found: {DocumentId}", request.DocumentId);
                return new DownloadContractDocumentResult
                {
                    Success = false,
                    ErrorMessage = $"Document not found with ID: {request.DocumentId}"
                };
            }

            logger.LogInformation(
                "Found document: {DocumentName} - FileUrl: {FileUrl}",
                document.DocumentName,
                document.FileUrl);

            var (downloadSuccess, fileStream, downloadError) = await s3Service.DownloadFileAsync(
                document.FileUrl,
                cancellationToken);

            if (!downloadSuccess || fileStream == null)
            {
                logger.LogError(
                    "Failed to download file from S3: {Error}",
                    downloadError);

                return new DownloadContractDocumentResult
                {
                    Success = false,
                    ErrorMessage = downloadError ?? "Failed to download file from S3"
                };
            }

            logger.LogInformation(
                "Successfully downloaded file from S3: {FileName} ({FileSize} bytes)",
                document.DocumentName,
                document.FileSize);

            return new DownloadContractDocumentResult
            {
                Success = true,
                FileStream = fileStream,
                FileName = document.DocumentName,
                FileSize = document.FileSize
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading contract document: {DocumentId}", request.DocumentId);
            return new DownloadContractDocumentResult
            {
                Success = false,
                ErrorMessage = $"Error downloading document: {ex.Message}"
            };
        }
    }
}