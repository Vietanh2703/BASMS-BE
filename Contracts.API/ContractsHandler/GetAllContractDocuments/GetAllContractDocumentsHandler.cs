namespace Contracts.API.ContractsHandler.GetAllContractDocuments;

// ================================================================
// QUERY & RESULT
// ================================================================

/// <summary>
/// Query để lấy tất cả contract documents từ S3
/// </summary>
public record GetAllContractDocumentsQuery : IQuery<GetAllContractDocumentsResult>;

/// <summary>
/// DTO cho Contract Document
/// </summary>
public record ContractDocumentDto
{
    public Guid Id { get; init; }
    public string Category { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public string DocumentName { get; init; } = string.Empty;
    public string FileUrl { get; init; } = string.Empty;
    public long? FileSize { get; init; }
    public string Version { get; init; } = string.Empty;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public Guid? UploadedBy { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Kích thước file dạng human-readable (e.g., "2.5 MB")
    /// </summary>
    public string FileSizeFormatted => FileSize.HasValue
        ? FormatFileSize(FileSize.Value)
        : "Unknown";

    /// <summary>
    /// Download URL
    /// </summary>
    public string DownloadUrl => $"/api/contracts/documents/{Id}/download";

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Kết quả query
/// </summary>
public record GetAllContractDocumentsResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<ContractDocumentDto> Documents { get; init; } = new();
    public int TotalCount { get; init; }
}

/// <summary>
/// Handler để lấy tất cả contract documents từ database (files lưu trên S3)
/// </summary>
internal class GetAllContractDocumentsHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetAllContractDocumentsHandler> logger)
    : IQueryHandler<GetAllContractDocumentsQuery, GetAllContractDocumentsResult>
{
    public async Task<GetAllContractDocumentsResult> Handle(
        GetAllContractDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting all contract documents from S3");

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // GET ALL DOCUMENTS FROM DATABASE
            // ================================================================
            var query = @"
                SELECT
                    Id,
                    Category,
                    DocumentType,
                    DocumentName,
                    FileUrl,
                    FileSize,
                    Version,
                    StartDate,
                    EndDate,
                    UploadedBy,
                    CreatedAt
                FROM contract_documents
                WHERE IsDeleted = 0
                ORDER BY CreatedAt DESC
            ";

            var documents = await connection.QueryAsync<Models.ContractDocument>(query);
            var documentsList = documents.ToList();

            logger.LogInformation("Found {Count} documents on S3", documentsList.Count);

            // ================================================================
            // MAP TO DTOs
            // ================================================================
            var documentDtos = documentsList.Select(d => new ContractDocumentDto
            {
                Id = d.Id,
                Category = d.Category,
                DocumentType = d.DocumentType,
                DocumentName = d.DocumentName,
                FileUrl = d.FileUrl,
                FileSize = d.FileSize,
                Version = d.Version,
                StartDate = d.StartDate,
                EndDate = d.EndDate,
                UploadedBy = d.UploadedBy,
                CreatedAt = d.CreatedAt
            }).ToList();

            return new GetAllContractDocumentsResult
            {
                Success = true,
                Documents = documentDtos,
                TotalCount = documentDtos.Count
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all contract documents");
            return new GetAllContractDocumentsResult
            {
                Success = false,
                ErrorMessage = $"Error getting documents: {ex.Message}",
                Documents = new List<ContractDocumentDto>()
            };
        }
    }
}