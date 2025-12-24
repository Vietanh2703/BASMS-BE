using Dapper;

namespace Incidents.API.IncidentHandler.GetAllIncidents;

/// <summary>
/// Query để lấy danh sách tất cả incidents
/// Sắp xếp theo: IncidentTime giảm dần (sự cố mới nhất trước)
/// </summary>
public record GetAllIncidentsQuery() : IQuery<GetAllIncidentsResult>;

/// <summary>
/// Result chứa danh sách incidents
/// </summary>
public record GetAllIncidentsResult
{
    public bool Success { get; init; }
    public List<IncidentDto> Incidents { get; init; } = new();
    public int TotalCount { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO cho incident
/// </summary>
public record IncidentDto
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
    public Guid? LocationId { get; init; }

    // Linked Shift
    public Guid? ShiftId { get; init; }
    public Guid? ShiftAssignmentId { get; init; }

    // Reporter Info
    public Guid ReporterId { get; init; }
    public string ReporterName { get; init; } = string.Empty;
    public string ReporterEmail { get; init; } = string.Empty;
    public DateTime ReportedTime { get; init; }

    // Status
    public string Status { get; init; } = string.Empty;

    // Response
    public string? ResponseContent { get; init; }
    public Guid? ResponderId { get; init; }
    public string? ResponderName { get; init; }
    public string? ResponderEmail { get; init; }
    public DateTime? RespondedAt { get; init; }

    // Audit
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? UpdatedBy { get; init; }
}

/// <summary>
/// Handler để lấy danh sách tất cả incidents
/// </summary>
internal class GetAllIncidentsHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetAllIncidentsHandler> logger)
    : IQueryHandler<GetAllIncidentsQuery, GetAllIncidentsResult>
{
    public async Task<GetAllIncidentsResult> Handle(
        GetAllIncidentsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting all incidents");

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // COUNT TOTAL RECORDS
            // ================================================================
            var countSql = @"
                SELECT COUNT(*)
                FROM incidents
                WHERE IsDeleted = 0";

            var totalCount = await connection.ExecuteScalarAsync<int>(countSql);

            logger.LogInformation(
                "Total incidents found: {TotalCount}",
                totalCount);

            // ================================================================
            // GET ALL DATA - SORTED BY INCIDENT TIME DESCENDING
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
                    LocationId,
                    ShiftId,
                    ShiftAssignmentId,
                    ReporterId,
                    ReporterName,
                    ReporterEmail,
                    ReportedTime,
                    Status,
                    ResponseContent,
                    ResponderId,
                    ResponderName,
                    ResponderEmail,
                    RespondedAt,
                    IsDeleted,
                    CreatedAt,
                    UpdatedAt,
                    CreatedBy,
                    UpdatedBy
                FROM incidents
                WHERE IsDeleted = 0
                ORDER BY
                    IncidentTime DESC";

            var incidents = await connection.QueryAsync<IncidentDto>(sql);
            var incidentsList = incidents.ToList();

            logger.LogInformation(
                "Retrieved {Count} incidents sorted by incident time (newest first)",
                incidentsList.Count);

            return new GetAllIncidentsResult
            {
                Success = true,
                Incidents = incidentsList,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all incidents");

            return new GetAllIncidentsResult
            {
                Success = false,
                Incidents = new List<IncidentDto>(),
                TotalCount = 0,
                ErrorMessage = $"Failed to get incidents: {ex.Message}"
            };
        }
    }
}
