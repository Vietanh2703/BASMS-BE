using Contracts.API.Extensions;

namespace Contracts.API.ContractsHandler.ViewDocumentByToken;

/// <summary>
/// Handler để lấy document bằng token bảo mật
/// Validate token expiry và quyền truy cập
/// </summary>
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
            logger.LogInformation("Validating token and retrieving document");

            // VALIDATION 1: Token không được rỗng
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

            // BƯỚC 1: TÌM DOCUMENT BẰNG TOKEN
            var document = await connection.QueryFirstOrDefaultAsync<ContractDocument>(@"
                SELECT * FROM contract_documents
                WHERE Tokens = @Token
                AND IsDeleted = 0
            ", new { Token = request.Token });

            // VALIDATION 2: Token không tồn tại hoặc document đã bị xóa
            if (document == null)
            {
                logger.LogWarning("Document not found for token: {Token}",
                    MaskToken(request.Token));
                return new ViewDocumentByTokenResult
                {
                    Success = false,
                    ErrorMessage = "Invalid security token or document not found",
                    ErrorCode = "INVALID_TOKEN"
                };
            }

            logger.LogInformation("Found document: {DocumentId} - {DocumentName}",
                document.Id, document.DocumentName);

            // VALIDATION 3: Token đã hết hạn
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

            // BƯỚC 2: DOWNLOAD FILE TỪ S3
            var (downloadSuccess, fileStream, downloadError) = await s3Service.DownloadFileAsync(
                document.FileUrl,
                cancellationToken);

            if (!downloadSuccess || fileStream == null)
            {
                logger.LogError("Failed to download file from S3: {FileUrl}. Error: {Error}",
                    document.FileUrl, downloadError);
                return new ViewDocumentByTokenResult
                {
                    Success = false,
                    ErrorMessage = "Failed to retrieve document from storage",
                    ErrorCode = "DOWNLOAD_FAILED"
                };
            }

            logger.LogInformation("Successfully downloaded file from S3: {FileUrl}",
                document.FileUrl);

            // BƯỚC 3: XÁC ĐỊNH CONTENT TYPE
            var contentType = DetermineContentType(document.DocumentName);

            // BƯỚC 4: LOG ACCESS (Audit Trail)
            await LogDocumentAccess(connection, document.Id, request.Token);

            return new ViewDocumentByTokenResult
            {
                Success = true,
                FileStream = fileStream,
                FileName = document.DocumentName,
                ContentType = contentType,
                FileSize = document.FileSize,
                DocumentId = document.Id
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

    /// <summary>
    /// Xác định Content-Type dựa trên extension
    /// </summary>
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

    /// <summary>
    /// Log document access để audit
    /// Có thể tạo table mới: document_access_log
    /// </summary>
    private async Task LogDocumentAccess(IDbConnection connection, Guid documentId, string token)
    {
        try
        {
            logger.LogInformation("Document {DocumentId} accessed with token {Token}",
                documentId, MaskToken(token));
        }
        catch (Exception ex)
        {
            // Không throw exception nếu log fail
            logger.LogWarning(ex, "Failed to log document access");
        }
    }

    /// <summary>
    /// Mask token để bảo mật trong log
    /// Ví dụ: abc123-def456-ghi789 → abc***ghi789
    /// </summary>
    private string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length < 10)
            return "***";

        return $"{token[..3]}***{token[^6..]}";
    }
}
