namespace Shifts.API.ShiftsHandler.GetAllShiftTemplateRequest;

public record GetAllShiftTemplateRequestQuery(
    Guid ManagerId
) : IQuery<GetAllShiftTemplateRequestResult>;

public record GetAllShiftTemplateRequestResult
{
    public bool Success { get; init; }
    public Guid ManagerId { get; init; }
    public int TotalContracts { get; init; }
    public int TotalTemplates { get; init; }
    public List<ContractTemplateGroupDto> ContractGroups { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

public record ContractTemplateGroupDto
{
    public Guid? ContractId { get; init; }
    public string ContractName { get; init; } = string.Empty;
    public int TemplateCount { get; init; }
    public List<ShiftTemplateDto> Templates { get; init; } = new();
    public int TotalLocations { get; init; }
    public List<string> LocationNames { get; init; } = new();
    public int TotalMinGuardsRequired { get; init; }
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
    public bool IsNightShift { get; init; }
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
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}


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
            
            var contractsWithCreatedShifts = await connection.QueryAsync<Guid?>(
                @"SELECT DISTINCT ContractId
                  FROM shift_templates
                  WHERE ManagerId = @ManagerId
                    AND Status = 'created_shift'
                    AND IsDeleted = 0
                    AND ContractId IS NOT NULL",
                new { request.ManagerId });

            var excludedContractIds = contractsWithCreatedShifts.ToHashSet();

            if (excludedContractIds.Any())
            {
                logger.LogInformation(
                    "Excluding {Count} contracts that have templates with status 'created_shift'",
                    excludedContractIds.Count);
                
                templatesList = templatesList
                    .Where(t => !t.ContractId.HasValue || !excludedContractIds.Contains(t.ContractId))
                    .ToList();

                logger.LogInformation(
                    "After filtering: {Count} templates remaining",
                    templatesList.Count);
            }

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
