namespace Contracts.API.LocationsHandler.GetLocationsByContractId;

// ================================================================
// QUERY & RESULT
// ================================================================

/// <summary>
/// Query để lấy tất cả locations của một contract
/// </summary>
public record GetLocationsByContractIdQuery(Guid ContractId) : IQuery<GetLocationsByContractIdResult>;

/// <summary>
/// DTO cho Location details
/// </summary>
public record LocationDto
{
    // From customer_locations
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public string LocationCode { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string LocationType { get; init; } = string.Empty;

    // Address
    public string Address { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? District { get; init; }
    public string? Ward { get; init; }

    // Geofencing
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public int GeofenceRadiusMeters { get; init; }
    public decimal? AltitudeMeters { get; init; }

    // Contact
    public string? SiteManagerName { get; init; }
    public string? SiteManagerPhone { get; init; }
    public string? EmergencyContactName { get; init; }
    public string? EmergencyContactPhone { get; init; }

    // Operating
    public string OperatingHoursType { get; init; } = string.Empty;
    public decimal? TotalAreaSqm { get; init; }
    public int? BuildingFloors { get; init; }

    // Schedule
    public bool FollowsStandardWorkweek { get; init; }
    public string? CustomWeekendDays { get; init; }

    // Requirements
    public bool Requires24x7Coverage { get; init; }
    public bool AllowsSingleGuard { get; init; }
    public int MinimumGuardsRequired { get; init; }

    // From contract_locations
    public Guid ContractLocationId { get; init; }
    public int GuardsRequired { get; init; }
    public string CoverageType { get; init; } = string.Empty;
    public DateTime ServiceStartDate { get; init; }
    public DateTime? ServiceEndDate { get; init; }
    public bool IsPrimaryLocation { get; init; }
    public int PriorityLevel { get; init; }
    public bool AutoGenerateShifts { get; init; }

    // Status
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Kết quả query
/// </summary>
public record GetLocationsByContractIdResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? ContractId { get; init; }
    public string? ContractNumber { get; init; }
    public int TotalLocations { get; init; }
    public List<LocationDto> Locations { get; init; } = new();
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// Handler để lấy tất cả locations của một contract
/// </summary>
internal class GetLocationsByContractIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetLocationsByContractIdHandler> logger)
    : IQueryHandler<GetLocationsByContractIdQuery, GetLocationsByContractIdResult>
{
    public async Task<GetLocationsByContractIdResult> Handle(
        GetLocationsByContractIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting locations for contract: {ContractId}", request.ContractId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // 1. CHECK IF CONTRACT EXISTS
            // ================================================================
            var contractQuery = @"
                SELECT ContractNumber
                FROM contracts
                WHERE Id = @ContractId AND IsDeleted = 0
            ";

            var contractNumber = await connection.QuerySingleOrDefaultAsync<string>(
                contractQuery,
                new { ContractId = request.ContractId });

            if (contractNumber == null)
            {
                logger.LogWarning("Contract not found: {ContractId}", request.ContractId);
                return new GetLocationsByContractIdResult
                {
                    Success = false,
                    ErrorMessage = $"Contract with ID {request.ContractId} not found"
                };
            }

            // ================================================================
            // 2. GET ALL LOCATIONS WITH FULL DETAILS (JOIN)
            // ================================================================
            var locationsQuery = @"
                SELECT
                    -- From customer_locations
                    cl.Id,
                    cl.CustomerId,
                    cl.LocationCode,
                    cl.LocationName,
                    cl.LocationType,
                    cl.Address,
                    cl.City,
                    cl.District,
                    cl.Ward,
                    cl.Latitude,
                    cl.Longitude,
                    cl.GeofenceRadiusMeters,
                    cl.AltitudeMeters,
                    cl.SiteManagerName,
                    cl.SiteManagerPhone,
                    cl.EmergencyContactName,
                    cl.EmergencyContactPhone,
                    cl.OperatingHoursType,
                    cl.TotalAreaSqm,
                    cl.BuildingFloors,
                    cl.FollowsStandardWorkweek,
                    cl.CustomWeekendDays,
                    cl.Requires24x7Coverage,
                    cl.AllowsSingleGuard,
                    cl.MinimumGuardsRequired,
                    cl.IsActive,
                    cl.CreatedAt,
                    cl.UpdatedAt,

                    -- From contract_locations
                    crl.Id AS ContractLocationId,
                    crl.GuardsRequired,
                    crl.CoverageType,
                    crl.ServiceStartDate,
                    crl.ServiceEndDate,
                    crl.IsPrimaryLocation,
                    crl.PriorityLevel,
                    crl.AutoGenerateShifts
                FROM contract_locations crl
                INNER JOIN customer_locations cl ON crl.LocationId = cl.Id
                WHERE crl.ContractId = @ContractId
                  AND crl.IsDeleted = 0
                  AND cl.IsDeleted = 0
                ORDER BY crl.IsPrimaryLocation DESC, crl.PriorityLevel ASC, cl.LocationName ASC
            ";

            var locations = await connection.QueryAsync<LocationDto>(
                locationsQuery,
                new { ContractId = request.ContractId });

            var locationsList = locations.ToList();

            logger.LogInformation(
                "Successfully retrieved {Count} location(s) for contract {ContractNumber}",
                locationsList.Count, contractNumber);

            return new GetLocationsByContractIdResult
            {
                Success = true,
                ContractId = request.ContractId,
                ContractNumber = contractNumber,
                TotalLocations = locationsList.Count,
                Locations = locationsList
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting locations for contract: {ContractId}", request.ContractId);
            return new GetLocationsByContractIdResult
            {
                Success = false,
                ErrorMessage = $"Error getting locations: {ex.Message}"
            };
        }
    }
}
