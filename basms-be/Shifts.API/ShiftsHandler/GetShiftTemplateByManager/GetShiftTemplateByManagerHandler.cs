namespace Shifts.API.ShiftsHandler.GetShiftTemplateByManager;

public record GetShiftTemplateByManagerQuery(
    Guid ManagerId,
    string? Status = null,
    bool? IsActive = null
) : IQuery<GetShiftTemplateByManagerResult>;

public record GetShiftTemplateByManagerResult
{
    public bool Success { get; init; }
    public List<ShiftTemplateDto> Templates { get; init; } = new();
    public int TotalCount { get; init; }
    public string? ErrorMessage { get; init; }
}

public record ShiftTemplateDto
{
    public Guid Id { get; init; }
    public Guid? ManagerId { get; init; }
    public Guid? ContractId { get; init; }
    public string TemplateCode { get; init; } = string.Empty;
    public string TemplateName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public decimal DurationHours { get; init; }
    public int BreakDurationMinutes { get; init; }
    public int PaidBreakMinutes { get; init; }
    public int UnpaidBreakMinutes { get; init; }
    public bool IsNightShift { get; init; }
    public bool IsOvernight { get; init; }
    public bool CrossesMidnight { get; init; }
    public bool AppliesMonday { get; init; }
    public bool AppliesTuesday { get; init; }
    public bool AppliesWednesday { get; init; }
    public bool AppliesThursday { get; init; }
    public bool AppliesFriday { get; init; }
    public bool AppliesSaturday { get; init; }
    public bool AppliesSunday { get; init; }
    public int MinGuardsRequired { get; init; }
    public int? MaxGuardsAllowed { get; init; }
    public int? OptimalGuards { get; init; }
    public Guid? LocationId { get; init; }
    public string? LocationName { get; init; }
    public string? LocationAddress { get; init; }
    public decimal? LocationLatitude { get; init; }
    public decimal? LocationLongitude { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? UpdatedBy { get; init; }
}


internal class GetShiftTemplateByManagerHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetShiftTemplateByManagerHandler> logger)
    : IQueryHandler<GetShiftTemplateByManagerQuery, GetShiftTemplateByManagerResult>
{
    public async Task<GetShiftTemplateByManagerResult> Handle(
        GetShiftTemplateByManagerQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting shift templates for Manager {ManagerId} with Status={Status}, IsActive={IsActive}",
                request.ManagerId,
                request.Status ?? "ALL",
                request.IsActive?.ToString() ?? "ALL");

            using var connection = await dbFactory.CreateConnectionAsync();

            var whereClauses = new List<string>
            {
                "ManagerId = @ManagerId",
                "IsDeleted = 0"
            };

            var parameters = new DynamicParameters();
            parameters.Add("ManagerId", request.ManagerId);

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                whereClauses.Add("Status = @Status");
                parameters.Add("Status", request.Status);
            }

            if (request.IsActive.HasValue)
            {
                whereClauses.Add("IsActive = @IsActive");
                parameters.Add("IsActive", request.IsActive.Value);
            }

            var whereClause = string.Join(" AND ", whereClauses);
            var sql = $@"
                SELECT
                    Id,
                    ManagerId,
                    ContractId,
                    TemplateCode,
                    TemplateName,
                    Description,
                    StartTime,
                    EndTime,
                    DurationHours,
                    BreakDurationMinutes,
                    PaidBreakMinutes,
                    UnpaidBreakMinutes,
                    IsNightShift,
                    IsOvernight,
                    CrossesMidnight,
                    AppliesMonday,
                    AppliesTuesday,
                    AppliesWednesday,
                    AppliesThursday,
                    AppliesFriday,
                    AppliesSaturday,
                    AppliesSunday,
                    MinGuardsRequired,
                    MaxGuardsAllowed,
                    OptimalGuards,
                    LocationId,
                    LocationName,
                    LocationAddress,
                    LocationLatitude,
                    LocationLongitude,
                    Status,
                    IsActive,
                    EffectiveFrom,
                    EffectiveTo,
                    CreatedAt,
                    UpdatedAt,
                    CreatedBy,
                    UpdatedBy
                FROM shift_templates
                WHERE {whereClause}
                ORDER BY CreatedAt DESC";

            var templates = await connection.QueryAsync<ShiftTemplateDto>(sql, parameters);
            var templatesList = templates.ToList();

            logger.LogInformation(
                "Found {Count} shift templates for Manager {ManagerId}",
                templatesList.Count,
                request.ManagerId);

            return new GetShiftTemplateByManagerResult
            {
                Success = true,
                Templates = templatesList,
                TotalCount = templatesList.Count
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting shift templates for Manager {ManagerId}",
                request.ManagerId);

            return new GetShiftTemplateByManagerResult
            {
                Success = false,
                Templates = new List<ShiftTemplateDto>(),
                TotalCount = 0,
                ErrorMessage = $"Failed to get shift templates: {ex.Message}"
            };
        }
    }
}
