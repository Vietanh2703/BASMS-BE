using Dapper;

namespace Incidents.API.IncidentHandler.GetAllIncidents;

/// <summary>
/// Query để lấy danh sách tất cả incidents
/// Sắp xếp theo: IncidentTime giảm dần (sự cố mới nhất trước)
/// </summary>
public record GetAllIncidentsQuery(
    Guid? ReporterId = null,
    Guid? ResponderId = null,
    Guid? ShiftId = null,
    Guid? LocationId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Status = null,
    string? IncidentType = null,
    string? Severity = null
) : IQuery<GetAllIncidentsResult>;

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
    public string? ReporterRole { get; init; }
    public DateTime ReportedTime { get; init; }

    // Status
    public string Status { get; init; } = string.Empty;

    // Response
    public string? ResponseContent { get; init; }
    public Guid? ResponderId { get; init; }
    public string? ResponderName { get; init; }
    public string? ResponderEmail { get; init; }
    public string? ResponderRole { get; init; }
    public DateTime? RespondedAt { get; init; }

    // Audit
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? UpdatedBy { get; init; }
}

/// <summary>
/// Handler để lấy danh sách tất cả incidents với filtering
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
            logger.LogInformation(
                "Getting all incidents: ReporterId={ReporterId}, FromDate={FromDate}, ToDate={ToDate}, Status={Status}, Severity={Severity}",
                request.ReporterId?.ToString() ?? "ALL",
                request.FromDate?.ToString("yyyy-MM-dd") ?? "ALL",
                request.ToDate?.ToString("yyyy-MM-dd") ?? "ALL",
                request.Status ?? "ALL",
                request.Severity ?? "ALL");

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BUILD DYNAMIC SQL QUERY
            // ================================================================
            var whereClauses = new List<string> { "IsDeleted = 0" };
            var parameters = new DynamicParameters();

            if (request.ReporterId.HasValue)
            {
                whereClauses.Add("ReporterId = @ReporterId");
                parameters.Add("ReporterId", request.ReporterId.Value);
            }

            if (request.ResponderId.HasValue)
            {
                whereClauses.Add("ResponderId = @ResponderId");
                parameters.Add("ResponderId", request.ResponderId.Value);
            }

            if (request.ShiftId.HasValue)
            {
                whereClauses.Add("ShiftId = @ShiftId");
                parameters.Add("ShiftId", request.ShiftId.Value);
            }

            if (request.LocationId.HasValue)
            {
                whereClauses.Add("LocationId = @LocationId");
                parameters.Add("LocationId", request.LocationId.Value);
            }

            if (request.FromDate.HasValue)
            {
                whereClauses.Add("DATE(IncidentTime) >= @FromDate");
                parameters.Add("FromDate", request.FromDate.Value.Date);
            }

            if (request.ToDate.HasValue)
            {
                whereClauses.Add("DATE(IncidentTime) <= @ToDate");
                parameters.Add("ToDate", request.ToDate.Value.Date);
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                whereClauses.Add("Status = @Status");
                parameters.Add("Status", request.Status);
            }

            if (!string.IsNullOrWhiteSpace(request.IncidentType))
            {
                whereClauses.Add("IncidentType = @IncidentType");
                parameters.Add("IncidentType", request.IncidentType);
            }

            if (!string.IsNullOrWhiteSpace(request.Severity))
            {
                whereClauses.Add("Severity = @Severity");
                parameters.Add("Severity", request.Severity);
            }

            var whereClause = string.Join(" AND ", whereClauses);

            // ================================================================
            // COUNT TOTAL RECORDS
            // ================================================================
            var countSql = $@"
                SELECT COUNT(*)
                FROM incidents
                WHERE {whereClause}";

            var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

            logger.LogInformation(
                "Total incidents found: {TotalCount}",
                totalCount);

            // ================================================================
            // GET ALL DATA - SORTED BY INCIDENT TIME DESCENDING
            // ================================================================

            var sql = $@"
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
                    ReporterRole,
                    ReportedTime,
                    Status,
                    ResponseContent,
                    ResponderId,
                    ResponderName,
                    ResponderEmail,
                    ResponderRole,
                    RespondedAt,
                    IsDeleted,
                    CreatedAt,
                    UpdatedAt,
                    CreatedBy,
                    UpdatedBy
                FROM incidents
                WHERE {whereClause}
                ORDER BY
                    IncidentTime DESC";

            var incidents = await connection.QueryAsync<IncidentDto>(sql, parameters);
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
