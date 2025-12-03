using Dapper;

namespace Shifts.API.ShiftsHandler.GetAllShiftTemplateRequest;

/// <summary>
/// Query để lấy tất cả shift templates có status = "await_create_shift"
/// Grouped by ContractId
/// </summary>
public record GetAllShiftTemplateRequestQuery(
    Guid ManagerId
) : IQuery<GetAllShiftTemplateRequestResult>;

/// <summary>
/// Result chứa danh sách shift templates grouped by contract
/// </summary>
public record GetAllShiftTemplateRequestResult
{
    public bool Success { get; init; }
    public Guid ManagerId { get; init; }
    public int TotalContracts { get; init; }
    public int TotalTemplates { get; init; }
    public List<ContractTemplateGroupDto> ContractGroups { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO cho mỗi contract group
/// </summary>
public record ContractTemplateGroupDto
{
    public Guid? ContractId { get; init; }
    public string ContractName { get; init; } = string.Empty;
    public int TemplateCount { get; init; }
    public List<ShiftTemplateDto> Templates { get; init; } = new();

    // Summary statistics
    public int TotalLocations { get; init; }
    public List<string> LocationNames { get; init; } = new();
    public int TotalMinGuardsRequired { get; init; }
}

/// <summary>
/// DTO cho shift template
/// </summary>
public record ShiftTemplateDto
{
    public Guid Id { get; init; }
    public Guid? ManagerId { get; init; }
    public Guid? ContractId { get; init; }
    public string TemplateCode { get; init; } = string.Empty;
    public string TemplateName { get; init; } = string.Empty;
    public string? Description { get; init; }

    // Time
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public decimal DurationHours { get; init; }
    public int BreakDurationMinutes { get; init; }

    // Classification
    public bool IsNightShift { get; init; }
    public bool CrossesMidnight { get; init; }

    // Days of week
    public bool AppliesMonday { get; init; }
    public bool AppliesTuesday { get; init; }
    public bool AppliesWednesday { get; init; }
    public bool AppliesThursday { get; init; }
    public bool AppliesFriday { get; init; }
    public bool AppliesSaturday { get; init; }
    public bool AppliesSunday { get; init; }

    // Staffing
    public int MinGuardsRequired { get; init; }
    public int? MaxGuardsAllowed { get; init; }
    public int? OptimalGuards { get; init; }

    // Location
    public Guid? LocationId { get; init; }
    public string? LocationName { get; init; }
    public string? LocationAddress { get; init; }

    // Status
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }

    // Audit
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Handler để lấy tất cả shift templates với status "await_create_shift"
/// và group theo ContractId
/// </summary>
internal class GetAllShiftTemplateRequestHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetAllShiftTemplateRequestHandler> logger)
    : IQueryHandler<GetAllShiftTemplateRequestQuery, GetAllShiftTemplateRequestResult>
{
    public async Task<GetAllShiftTemplateRequestResult> Handle(
        GetAllShiftTemplateRequestQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting all shift templates with status 'await_create_shift' for Manager {ManagerId}",
                request.ManagerId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: VALIDATE MANAGER EXISTS
            // ================================================================
            var manager = await connection.QueryFirstOrDefaultAsync<Managers>(
                @"SELECT * FROM managers
                  WHERE Id = @ManagerId
                  AND IsDeleted = 0
                  AND IsActive = 1",
                new { request.ManagerId });

            if (manager == null)
            {
                logger.LogWarning("Manager {ManagerId} not found or inactive", request.ManagerId);
                return new GetAllShiftTemplateRequestResult
                {
                    Success = false,
                    ManagerId = request.ManagerId,
                    TotalContracts = 0,
                    TotalTemplates = 0,
                    ContractGroups = new List<ContractTemplateGroupDto>(),
                    ErrorMessage = "Manager not found or inactive"
                };
            }

            // ================================================================
            // BƯỚC 2: GET ALL TEMPLATES WITH STATUS "await_create_shift"
            // ================================================================
            var sql = @"
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
                    IsNightShift,
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
                    Status,
                    IsActive,
                    EffectiveFrom,
                    EffectiveTo,
                    CreatedAt,
                    UpdatedAt
                FROM shift_templates
                WHERE ManagerId = @ManagerId
                  AND Status = 'await_create_shift'
                  AND IsDeleted = 0
                ORDER BY ContractId, CreatedAt DESC";

            var templates = await connection.QueryAsync<ShiftTemplateDto>(sql, new { request.ManagerId });
            var templatesList = templates.ToList();

            logger.LogInformation(
                "Found {Count} shift templates with status 'await_create_shift'",
                templatesList.Count);

            // ================================================================
            // BƯỚC 3: GROUP BY ContractId
            // ================================================================
            var contractGroups = templatesList
                .GroupBy(t => t.ContractId)
                .Select(group => new ContractTemplateGroupDto
                {
                    ContractId = group.Key,
                    ContractName = group.Key.HasValue
                        ? $"Contract {group.Key.Value.ToString()[..8]}"
                        : "No Contract",
                    TemplateCount = group.Count(),
                    Templates = group.ToList(),

                    // Calculate summary statistics
                    TotalLocations = group
                        .Where(t => t.LocationId.HasValue)
                        .Select(t => t.LocationId)
                        .Distinct()
                        .Count(),

                    LocationNames = group
                        .Where(t => !string.IsNullOrWhiteSpace(t.LocationName))
                        .Select(t => t.LocationName!)
                        .Distinct()
                        .ToList(),

                    TotalMinGuardsRequired = group.Sum(t => t.MinGuardsRequired)
                })
                .OrderByDescending(g => g.TemplateCount)
                .ToList();

            logger.LogInformation(
                "Grouped into {ContractCount} contracts with total {TemplateCount} templates",
                contractGroups.Count,
                templatesList.Count);

            // ================================================================
            // BƯỚC 4: RETURN RESULT
            // ================================================================
            return new GetAllShiftTemplateRequestResult
            {
                Success = true,
                ManagerId = request.ManagerId,
                TotalContracts = contractGroups.Count,
                TotalTemplates = templatesList.Count,
                ContractGroups = contractGroups
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting shift templates for Manager {ManagerId}",
                request.ManagerId);

            return new GetAllShiftTemplateRequestResult
            {
                Success = false,
                ManagerId = request.ManagerId,
                TotalContracts = 0,
                TotalTemplates = 0,
                ContractGroups = new List<ContractTemplateGroupDto>(),
                ErrorMessage = $"Failed to get shift templates: {ex.Message}"
            };
        }
    }
}
