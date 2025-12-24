namespace Contracts.API.ContractsHandler.ViewDocumentByToken;


internal class ViewDocumentByTokenHandler(
    IDbConnectionFactory connectionFactory,
    IS3Service s3Service,
    ILogger<ViewDocumentByTokenHandler> logger)
    : IQueryHandler<ViewDocumentByTokenQuery, ViewDocumentByTokenResult>
{
    public async Task<ViewDocumentByTokenResult> Handle(
        ViewDocumentByTokenQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Validating documentId {DocumentId} and token", request.DocumentId);


            if (string.IsNullOrWhiteSpace(request.Token))
            {
                logger.LogWarning("Token is null or empty");
                return new ViewDocumentByTokenResult
                {
                    Success = false,
                    ErrorMessage = "Security token is required",
                    ErrorCode = "TOKEN_REQUIRED"
                };
            }

            using var connection = await connectionFactory.CreateConnectionAsync();


            var document = await connection.QueryFirstOrDefaultAsync<ContractDocument>(@"
                SELECT * FROM contract_documents
                WHERE Id = @DocumentId
                AND Tokens = @Token
                AND IsDeleted = 0
            ", new { DocumentId = request.DocumentId, Token = request.Token });


            if (document == null)
            {
                logger.LogWarning("Document not found or token mismatch. DocumentId: {DocumentId}, Token: {Token}",
                    request.DocumentId, MaskToken(request.Token));
                return new ViewDocumentByTokenResult
                {
                    Success = false,
                    ErrorMessage = "Invalid document ID or security token",
                    ErrorCode = "INVALID_TOKEN"
                };
            }

            logger.LogInformation("Found document: {DocumentId} - {DocumentName}",
                document.Id, document.DocumentName);


            if (document.TokenExpiredDay < DateTime.UtcNow)
            {
                logger.LogWarning("Token expired. Expiry: {ExpiryDate}, Current: {CurrentDate}",
                    document.TokenExpiredDay, DateTime.UtcNow);
                return new ViewDocumentByTokenResult
                {
                    Success = false,
                    ErrorMessage = $"Security token has expired on {document.TokenExpiredDay:yyyy-MM-dd HH:mm:ss} UTC",
                    ErrorCode = "TOKEN_EXPIRED"
                };
            }

            logger.LogInformation("Token is valid. Expires: {ExpiryDate}",
                document.TokenExpiredDay);
            
            string presignedUrl;
            try
            {
                presignedUrl = s3Service.GetPresignedUrl(document.FileUrl, expirationMinutes: 15);
                logger.LogInformation("Generated pre-signed URL for document {DocumentId}", document.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate pre-signed URL for {FileUrl}", document.FileUrl);
                return new ViewDocumentByTokenResult
                {
                    Success = false,
                    ErrorMessage = "Failed to generate document access URL",
                    ErrorCode = "URL_GENERATION_FAILED"
                };
            }
            
            var contentType = DetermineContentType(document.DocumentName);
            
            await LogDocumentAccess(connection, document.Id, request.Token);

            var urlExpiresAt = DateTime.UtcNow.AddMinutes(15);

            return new ViewDocumentByTokenResult
            {
                Success = true,
                FileUrl = presignedUrl,
                FileName = document.DocumentName,
                ContentType = contentType,
                FileSize = document.FileSize,
                DocumentId = document.Id,
                UrlExpiresAt = urlExpiresAt
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving document by token");
            return new ViewDocumentByTokenResult
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

    private async Task LogDocumentAccess(IDbConnection connection, Guid documentId, string token)
    {
        try
        {
            logger.LogInformation("Document {DocumentId} accessed with token {Token}",
                documentId, MaskToken(token));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log document access");
        }
    }


    private string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length < 10)
            return "***";

        return $"{token[..3]}***{token[^6..]}";
    }
}
