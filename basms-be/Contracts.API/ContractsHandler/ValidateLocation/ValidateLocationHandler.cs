namespace Contracts.API.ContractsHandler.ValidateLocation;

// Query để validate location thuộc contract
public record ValidateLocationQuery(Guid ContractId, Guid LocationId) : IQuery<ValidateLocationResult>;

// Result
public record ValidateLocationResult(
    bool IsValid,
    bool Exists,
    bool IsActive,
    bool BelongsToContract,
    string? ErrorMessage,
    LocationDto? Location
);

// DTO
public record LocationDto(
    Guid Id,
    string LocationCode,
    string LocationName,
    string LocationType,
    string Address,
    decimal? Latitude,
    decimal? Longitude,
    int GeofenceRadiusMeters,
    bool IsActive,
    int MinimumGuardsRequired
);

internal class ValidateLocationHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ValidateLocationHandler> logger)
    : IQueryHandler<ValidateLocationQuery, ValidateLocationResult>
{
    public async Task<ValidateLocationResult> Handle(
        ValidateLocationQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Validating location {LocationId} for contract {ContractId}",
                request.LocationId,
                request.ContractId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // Lấy location
            var location = await connection.GetAsync<CustomerLocation>(request.LocationId);

            if (location == null || location.IsDeleted)
            {
                return new ValidateLocationResult(
                    IsValid: false,
                    Exists: false,
                    IsActive: false,
                    BelongsToContract: false,
                    ErrorMessage: "Location not found",
                    Location: null
                );
            }

            // Check location active
            if (!location.IsActive)
            {
                return new ValidateLocationResult(
                    IsValid: false,
                    Exists: true,
                    IsActive: false,
                    BelongsToContract: false,
                    ErrorMessage: "Location is not active",
                    Location: null
                );
            }

            // Check location thuộc contract
            var sql = @"SELECT * FROM contract_locations
                       WHERE ContractId = @ContractId
                       AND LocationId = @LocationId
                       AND IsDeleted = 0";

            var contractLocation = await connection.QueryFirstOrDefaultAsync<ContractLocation>(
                sql,
                new { request.ContractId, request.LocationId });

            if (contractLocation == null)
            {
                return new ValidateLocationResult(
                    IsValid: false,
                    Exists: true,
                    IsActive: true,
                    BelongsToContract: false,
                    ErrorMessage: "Location does not belong to this contract",
                    Location: null
                );
            }

            // Map to DTO
            var locationDto = new LocationDto(
                Id: location.Id,
                LocationCode: location.LocationCode,
                LocationName: location.LocationName,
                LocationType: location.LocationType,
                Address: location.Address,
                Latitude: location.Latitude,
                Longitude: location.Longitude,
                GeofenceRadiusMeters: location.GeofenceRadiusMeters,
                IsActive: location.IsActive,
                MinimumGuardsRequired: location.MinimumGuardsRequired
            );

            logger.LogInformation("Location {LocationCode} is valid for contract", location.LocationCode);

            return new ValidateLocationResult(
                IsValid: true,
                Exists: true,
                IsActive: true,
                BelongsToContract: true,
                ErrorMessage: null,
                Location: locationDto
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error validating location {LocationId} for contract {ContractId}",
                request.LocationId,
                request.ContractId);
            throw;
        }
    }
}
