namespace Contracts.API.LocationsHandler.GetLocationById;

// ================================================================
// QUERY & RESULT
// ================================================================

/// <summary>
/// Query để lấy chi tiết một location theo ID
/// </summary>
public record GetLocationByIdQuery(Guid LocationId) : IQuery<GetLocationByIdResult>;

/// <summary>
/// DTO cho Location detail (full info)
/// </summary>
public record LocationDetailDto
{
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

    // Status
    public bool IsActive { get; init; }
    public bool IsDeleted { get; init; }

    // Metadata
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? UpdatedBy { get; init; }
}

/// <summary>
/// Kết quả query
/// </summary>
public record GetLocationByIdResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public LocationDetailDto? Location { get; init; }
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// Handler để lấy chi tiết một location theo ID
/// </summary>
internal class GetLocationByIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetLocationByIdHandler> logger)
    : IQueryHandler<GetLocationByIdQuery, GetLocationByIdResult>
{
    public async Task<GetLocationByIdResult> Handle(
        GetLocationByIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting location by ID: {LocationId}", request.LocationId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // GET LOCATION BY ID
            // ================================================================
            var locationQuery = @"
                SELECT
                    Id,
                    CustomerId,
                    LocationCode,
                    LocationName,
                    LocationType,
                    Address,
                    City,
                    District,
                    Ward,
                    Latitude,
                    Longitude,
                    GeofenceRadiusMeters,
                    AltitudeMeters,
                    SiteManagerName,
                    SiteManagerPhone,
                    EmergencyContactName,
                    EmergencyContactPhone,
                    OperatingHoursType,
                    TotalAreaSqm,
                    BuildingFloors,
                    FollowsStandardWorkweek,
                    CustomWeekendDays,
                    Requires24x7Coverage,
                    AllowsSingleGuard,
                    MinimumGuardsRequired,
                    IsActive,
                    IsDeleted,
                    CreatedAt,
                    UpdatedAt,
                    CreatedBy,
                    UpdatedBy
                FROM customer_locations
                WHERE Id = @LocationId
            ";

            var location = await connection.QuerySingleOrDefaultAsync<LocationDetailDto>(
                locationQuery,
                new { LocationId = request.LocationId });

            if (location == null)
            {
                logger.LogWarning("Location not found: {LocationId}", request.LocationId);
                return new GetLocationByIdResult
                {
                    Success = false,
                    ErrorMessage = $"Location with ID {request.LocationId} not found"
                };
            }

            logger.LogInformation(
                "Successfully retrieved location: {LocationCode} - {LocationName}",
                location.LocationCode, location.LocationName);

            return new GetLocationByIdResult
            {
                Success = true,
                Location = location
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting location by ID: {LocationId}", request.LocationId);
            return new GetLocationByIdResult
            {
                Success = false,
                ErrorMessage = $"Error getting location: {ex.Message}"
            };
        }
    }
}
