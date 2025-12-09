namespace Contracts.API.ContractsHandler.GetDocumentById;

// ================================================================
// QUERY & RESULT
// ================================================================

/// <summary>
/// Query để lấy thông tin chi tiết Document theo ID
/// </summary>
public record GetDocumentByIdQuery(Guid DocumentId) : IQuery<GetDocumentByIdResult>;

/// <summary>
/// Kết quả chi tiết document
/// </summary>
public record GetDocumentByIdResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DocumentDetailDto? Document { get; init; }
}

/// <summary>
/// DTO chi tiết document
/// </summary>
public record DocumentDetailDto
{
    // Basic info
    public Guid Id { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string DocumentName { get; init; } = string.Empty;
    public string FileUrl { get; init; } = string.Empty;
    public long? FileSize { get; init; }
    public string Version { get; init; } = string.Empty;

    // Token info (for digital signature)
    public string? Tokens { get; init; }
    public DateTime? TokenExpiredDay { get; init; }

    // Document dates
    public DateTime? DocumentDate { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime? SignDate { get; init; }

    // Approval info
    public DateTime? ApprovedAt { get; init; }
    public Guid? ApprovedBy { get; init; }

    // Upload info
    public Guid? UploadedBy { get; init; }
    public string? DocumentEmail { get; init; }
    public string? DocumentCustomerName { get; init; }

    // Metadata
    public DateTime CreatedAt { get; init; }
}


// ================================================================
// HANDLER
// ================================================================

internal class GetDocumentByIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetDocumentByIdHandler> logger)
    : IQueryHandler<GetDocumentByIdQuery, GetDocumentByIdResult>
{
    public async Task<GetDocumentByIdResult> Handle(
        GetDocumentByIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting document details for DocumentId: {DocumentId}", request.DocumentId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // LẤY CONTRACT DOCUMENT
            // ================================================================
            var document = await connection.QueryFirstOrDefaultAsync<Models.ContractDocument>(
                "SELECT * FROM contract_documents WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.DocumentId });

            if (document == null)
            {
                logger.LogWarning("Document not found: {DocumentId}", request.DocumentId);
                return new GetDocumentByIdResult
                {
                    Success = false,
                    ErrorMessage = $"Document with ID {request.DocumentId} not found"
                };
            }

            // ================================================================
            // MAP TO DTO
            // ================================================================
            var result = new GetDocumentByIdResult
            {
                Success = true,
                Document = new DocumentDetailDto
                {
                    Id = document.Id,
                    DocumentType = document.DocumentType,
                    Category = document.Category,
                    DocumentName = document.DocumentName,
                    FileUrl = document.FileUrl,
                    FileSize = document.FileSize,
                    Version = document.Version,
                    Tokens = document.Tokens,
                    TokenExpiredDay = document.TokenExpiredDay,
                    DocumentDate = document.DocumentDate,
                    StartDate = document.StartDate,
                    EndDate = document.EndDate,
                    SignDate = document.SignDate,
                    ApprovedAt = document.ApprovedAt,
                    ApprovedBy = document.ApprovedBy,
                    UploadedBy = document.UploadedBy,
                    DocumentEmail = document.DocumentEmail,
                    DocumentCustomerName = document.DocumentCustomerName,
                    CreatedAt = document.CreatedAt
                }
            };

            logger.LogInformation(
                "Document details retrieved: {DocumentName} (Type: {DocumentType}, Version: {Version})",
                document.DocumentName, document.DocumentType, document.Version);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting document details for DocumentId: {DocumentId}", request.DocumentId);
            return new GetDocumentByIdResult
            {
                Success = false,
                ErrorMessage = $"Error retrieving document: {ex.Message}"
            };
        }
    }
}
