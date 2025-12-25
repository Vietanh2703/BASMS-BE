using Incidents.API.Extensions;

namespace Incidents.API.IncidentHandler.GetIncidentById;

public record GetIncidentByIdQuery(Guid IncidentId) : IQuery<GetIncidentByIdResult>;

public record GetIncidentByIdResult
{
    public bool Success { get; init; }
    public IncidentDetailDto? Incident { get; init; }
    public string? ErrorMessage { get; init; }
}

public record IncidentDetailDto
{
    public Guid Id { get; init; }
    public string IncidentCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    // Classification
    public string IncidentType { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;

    // Time & Location
    public DateTime IncidentTime { get; init; }
    public string Location { get; init; } = string.Empty;
    public string? ShiftLocation { get; init; }

    // Linked Shift
    public Guid? ShiftId { get; init; }
    public Guid? ShiftAssignmentId { get; init; }

    // Reporter Info
    public Guid ReporterId { get; init; }
    public DateTime ReportedTime { get; init; }

    // Status
    public string Status { get; init; } = string.Empty;

    // Response
    public string? ResponseContent { get; init; }
    public Guid? ResponderId { get; init; }
    public DateTime? RespondedAt { get; init; }

    // Media
    public List<IncidentMediaDto> MediaFiles { get; init; } = new();

    // Audit
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? UpdatedBy { get; init; }
}

public record IncidentMediaDto
{
    public Guid Id { get; init; }
    public string MediaType { get; init; } = string.Empty;
    public string FileUrl { get; init; } = string.Empty;
    public string? PresignedUrl { get; set; }
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string? MimeType { get; init; }
    public string? Caption { get; init; }
    public int DisplayOrder { get; init; }
    public Guid? UploadedBy { get; init; }
    public DateTime CreatedAt { get; init; }
}

internal class GetIncidentByIdHandler(
    IDbConnectionFactory dbFactory,
    IS3Service s3Service,
    ILogger<GetIncidentByIdHandler> logger)
    : IQueryHandler<GetIncidentByIdQuery, GetIncidentByIdResult>
{
    public async Task<GetIncidentByIdResult> Handle(
        GetIncidentByIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting incident details for IncidentId={IncidentId}",
                request.IncidentId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // GET INCIDENT DETAILS
            // ================================================================

            var sql = @"
                SELECT
                    Id,
                    IncidentCode,
                    Title,
                    Description,
                    IncidentType,
                    Severity,
                    IncidentTime,
                    Location,
                    ShiftLocation,
                    ShiftId,
                    ShiftAssignmentId,
                    ReporterId,
                    ReportedTime,
                    Status,
                    ResponseContent,
                    ResponderId,
                    RespondedAt,
                    IsDeleted,
                    CreatedAt,
                    UpdatedAt,
                    CreatedBy,
                    UpdatedBy
                FROM incidents
                WHERE Id = @IncidentId
                  AND IsDeleted = 0";

            var incident = await connection.QueryFirstOrDefaultAsync<IncidentDetailDto>(
                sql,
                new { IncidentId = request.IncidentId });

            if (incident == null)
            {
                logger.LogWarning(
                    "Incident not found: {IncidentId}",
                    request.IncidentId);

                return new GetIncidentByIdResult
                {
                    Success = false,
                    ErrorMessage = $"Incident with ID {request.IncidentId} not found"
                };
            }

            // ================================================================
            // GET MEDIA FILES
            // ================================================================

            var mediaSql = @"
                SELECT
                    Id,
                    MediaType,
                    FileUrl,
                    FileName,
                    FileSize,
                    MimeType,
                    Caption,
                    DisplayOrder,
                    UploadedBy,
                    CreatedAt
                FROM incident_media
                WHERE IncidentId = @IncidentId
                  AND IsDeleted = 0
                ORDER BY DisplayOrder, CreatedAt";

            var mediaFiles = await connection.QueryAsync<IncidentMediaDto>(
                mediaSql,
                new { IncidentId = request.IncidentId });

            var mediaFilesList = mediaFiles.ToList();

            // ================================================================
            // GENERATE PRE-SIGNED URLs FOR S3 FILES
            // ================================================================

            foreach (var media in mediaFilesList)
            {
                if (!string.IsNullOrEmpty(media.FileUrl))
                {
                    try
                    {
                        // Generate pre-signed URL valid for 60 minutes
                        var presignedUrl = s3Service.GetPresignedUrl(media.FileUrl, expirationMinutes: 60);
                        media.PresignedUrl = presignedUrl;

                        logger.LogDebug(
                            "Generated pre-signed URL for media {MediaId} ({FileName})",
                            media.Id,
                            media.FileName);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to generate pre-signed URL for media {MediaId} ({FileUrl})",
                            media.Id,
                            media.FileUrl);
                        // Keep original FileUrl if presigned URL generation fails
                        media.PresignedUrl = null;
                    }
                }
            }

            incident.MediaFiles.AddRange(mediaFilesList);

            logger.LogInformation(
                "Retrieved incident {IncidentCode} with {MediaCount} media files ({PresignedCount} with pre-signed URLs)",
                incident.IncidentCode,
                incident.MediaFiles.Count,
                incident.MediaFiles.Count(m => !string.IsNullOrEmpty(m.PresignedUrl)));

            return new GetIncidentByIdResult
            {
                Success = true,
                Incident = incident
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error getting incident by ID={IncidentId}",
                request.IncidentId);

            return new GetIncidentByIdResult
            {
                Success = false,
                ErrorMessage = $"Failed to get incident: {ex.Message}"
            };
        }
    }
}
