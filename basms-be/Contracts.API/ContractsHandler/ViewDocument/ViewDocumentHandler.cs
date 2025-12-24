namespace Contracts.API.ContractsHandler.ViewDocument;

internal class ViewDocumentHandler(
    IDbConnectionFactory connectionFactory,
    IS3Service s3Service,
    ILogger<ViewDocumentHandler> logger)
    : IQueryHandler<ViewDocumentQuery, ViewDocumentResult>
{
    public async Task<ViewDocumentResult> Handle(
        ViewDocumentQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Retrieving document by documentId: {DocumentId}", request.DocumentId);

            using var connection = await connectionFactory.CreateConnectionAsync();
            
            var document = await connection.QueryFirstOrDefaultAsync<ContractDocument>(@"
                SELECT * FROM contract_documents
                WHERE Id = @DocumentId
                AND IsDeleted = 0
            ", new { DocumentId = request.DocumentId });
            
            if (document == null)
            {
                logger.LogWarning("Document not found or deleted. DocumentId: {DocumentId}", request.DocumentId);
                return new ViewDocumentResult
                {
                    Success = false,
                    ErrorMessage = "Document not found",
                    ErrorCode = "DOCUMENT_NOT_FOUND"
                };
            }

            logger.LogInformation("Found document: {DocumentId} - {DocumentName}",
                document.Id, document.DocumentName);
            
            string presignedUrl;
            try
            {
                presignedUrl = s3Service.GetPresignedUrl(document.FileUrl, expirationMinutes: 15);
                logger.LogInformation("Generated pre-signed URL for document {DocumentId}", document.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate pre-signed URL for {FileUrl}", document.FileUrl);
                return new ViewDocumentResult
                {
                    Success = false,
                    ErrorMessage = "Failed to generate document access URL",
                    ErrorCode = "URL_GENERATION_FAILED"
                };
            }
            
            var contentType = DetermineContentType(document.DocumentName);
            
            await LogDocumentAccess(connection, document.Id);

            var urlExpiresAt = DateTime.UtcNow.AddMinutes(15);

            return new ViewDocumentResult
            {
                Success = true,
                FileUrl = presignedUrl,
                FileName = document.DocumentName,
                ContentType = contentType,
                FileSize = document.FileSize,
                DocumentId = document.Id,
                CreatedAt = document.CreatedAt,
                UrlExpiresAt = urlExpiresAt
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving document");
            return new ViewDocumentResult
            {
                Success = false,
                ErrorMessage = $"Failed to retrieve document: {ex.Message}",
                ErrorCode = "INTERNAL_ERROR"
            };
        }
    }
    
    private string DetermineContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
    
    private async Task LogDocumentAccess(IDbConnection connection, Guid documentId)
    {
        try
        {
            logger.LogInformation("Document {DocumentId} accessed (no token required)", documentId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log document access");
        }
    }
}
