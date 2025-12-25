namespace Incidents.API.IncidentHandler.GetAllIncidentsByReporterId;

public record GetAllIncidentsByReporterIdQuery(Guid ReporterId) : IQuery<GetAllIncidentsByReporterIdResult>;

public record GetAllIncidentsByReporterIdResult
{
    public bool Success { get; init; }
    public List<IncidentDto> Incidents { get; init; } = new();
    public int TotalCount { get; init; }
    public Guid ReporterId { get; init; }
    public string? ErrorMessage { get; init; }
}

public record IncidentDto
{
    public Guid Id { get; init; }
    public string IncidentCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string IncidentType { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public DateTime IncidentTime { get; init; }
    public string Location { get; init; } = string.Empty;
    public string? ShiftLocation { get; init; }
    public Guid? ShiftId { get; init; }
    public Guid? ShiftAssignmentId { get; init; }
    public Guid ReporterId { get; init; }
    public DateTime ReportedTime { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ResponseContent { get; init; }
    public Guid? ResponderId { get; init; }
    public DateTime? RespondedAt { get; init; }
    public List<IncidentMediaDto> MediaFiles { get; init; } = new();
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record IncidentMediaDto
{
    public Guid Id { get; init; }
    public string MediaType { get; init; } = string.Empty;
    public string FileUrl { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string? Caption { get; init; }
    public int DisplayOrder { get; init; }
}

internal class GetAllIncidentsByReporterIdHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetAllIncidentsByReporterIdHandler> logger)
    : IQueryHandler<GetAllIncidentsByReporterIdQuery, GetAllIncidentsByReporterIdResult>
{
    public async Task<GetAllIncidentsByReporterIdResult> Handle(
        GetAllIncidentsByReporterIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting all incidents for ReporterId={ReporterId}",
                request.ReporterId);

            using var connection = await dbFactory.CreateConnectionAsync();

            var countSql = @"
                SELECT COUNT(*)
                FROM incidents
                WHERE IsDeleted = 0
                  AND ReporterId = @ReporterId";

            var totalCount = await connection.ExecuteScalarAsync<int>(
                countSql,
                new { ReporterId = request.ReporterId });

            logger.LogInformation(
                "Total incidents found for reporter: {TotalCount}",
                totalCount);

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
                    UpdatedAt
                FROM incidents
                WHERE IsDeleted = 0
                  AND ReporterId = @ReporterId
                ORDER BY
                    IncidentTime DESC";

            var incidents = await connection.QueryAsync<IncidentDto>(
                sql,
                new { ReporterId = request.ReporterId });

            var incidentsList = incidents.ToList();
            

            if (incidentsList.Any())
            {
                var incidentIds = incidentsList.Select(i => i.Id).ToList();

                var mediaSql = @"
                    SELECT
                        Id,
                        IncidentId,
                        MediaType,
                        FileUrl,
                        FileName,
                        FileSize,
                        Caption,
                        DisplayOrder
                    FROM incident_media
                    WHERE IsDeleted = 0
                      AND IncidentId IN @IncidentIds
                    ORDER BY DisplayOrder";

                var mediaFiles = await connection.QueryAsync<dynamic>(
                    mediaSql,
                    new { IncidentIds = incidentIds });

                var mediaGrouped = mediaFiles
                    .GroupBy(m => (Guid)m.IncidentId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(m => new IncidentMediaDto
                        {
                            Id = m.Id,
                            MediaType = m.MediaType,
                            FileUrl = m.FileUrl,
                            FileName = m.FileName,
                            FileSize = m.FileSize,
                            Caption = m.Caption,
                            DisplayOrder = m.DisplayOrder
                        }).ToList()
                    );

                foreach (var incident in incidentsList)
                {
                    if (mediaGrouped.TryGetValue(incident.Id, out var media))
                    {
                        incident.MediaFiles.AddRange(media);
                    }
                }

                logger.LogInformation(
                    "Retrieved media files for {Count} incidents",
                    incidentsList.Count);
            }

            logger.LogInformation(
                "Retrieved {Count} incidents for reporter {ReporterId}",
                incidentsList.Count,
                request.ReporterId);

            return new GetAllIncidentsByReporterIdResult
            {
                Success = true,
                Incidents = incidentsList,
                TotalCount = totalCount,
                ReporterId = request.ReporterId
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error getting incidents for ReporterId={ReporterId}",
                request.ReporterId);

            return new GetAllIncidentsByReporterIdResult
            {
                Success = false,
                Incidents = new List<IncidentDto>(),
                TotalCount = 0,
                ReporterId = request.ReporterId,
                ErrorMessage = $"Failed to get incidents: {ex.Message}"
            };
        }
    }
}
