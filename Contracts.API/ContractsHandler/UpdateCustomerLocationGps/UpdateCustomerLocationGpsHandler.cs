namespace Contracts.API.ContractsHandler.UpdateCustomerLocationGps;

/// <summary>
/// Command để cập nhật GPS coordinates (Latitude, Longitude) của location
/// Chỉ cho phép cập nhật GPS, không cho thay đổi thông tin khác
/// </summary>
public record UpdateCustomerLocationGpsCommand(
    Guid LocationId,
    decimal Latitude,
    decimal Longitude
) : ICommand<UpdateCustomerLocationGpsResult>;

/// <summary>
/// Result của việc cập nhật GPS
/// </summary>
public record UpdateCustomerLocationGpsResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? LocationId { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public string? LocationName { get; init; }
    public string? Address { get; init; }
}

internal class UpdateCustomerLocationGpsHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<UpdateCustomerLocationGpsHandler> logger)
    : ICommandHandler<UpdateCustomerLocationGpsCommand, UpdateCustomerLocationGpsResult>
{
    public async Task<UpdateCustomerLocationGpsResult> Handle(
        UpdateCustomerLocationGpsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Updating GPS coordinates for location {LocationId} to ({Lat}, {Lng})",
                request.LocationId,
                request.Latitude,
                request.Longitude);

            // Validate GPS coordinates
            if (request.Latitude < -90 || request.Latitude > 90)
            {
                return new UpdateCustomerLocationGpsResult
                {
                    Success = false,
                    ErrorMessage = "Latitude must be between -90 and 90 degrees"
                };
            }

            if (request.Longitude < -180 || request.Longitude > 180)
            {
                return new UpdateCustomerLocationGpsResult
                {
                    Success = false,
                    ErrorMessage = "Longitude must be between -180 and 180 degrees"
                };
            }

            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Lấy location hiện tại
                var location = await connection.GetAsync<CustomerLocation>(request.LocationId, transaction);

                if (location == null || location.IsDeleted)
                {
                    return new UpdateCustomerLocationGpsResult
                    {
                        Success = false,
                        ErrorMessage = "Location not found"
                    };
                }

                // Lưu GPS cũ để log
                var oldLatitude = location.Latitude;
                var oldLongitude = location.Longitude;

                // Chỉ cập nhật Latitude, Longitude và UpdatedAt
                // KHÔNG cho phép thay đổi thông tin khác
                location.Latitude = request.Latitude;
                location.Longitude = request.Longitude;
                location.UpdatedAt = DateTime.UtcNow;

                await connection.UpdateAsync(location, transaction);

                transaction.Commit();

                logger.LogInformation(
                    "✓ Updated GPS for location {LocationCode} ({LocationName}): ({OldLat}, {OldLng}) → ({NewLat}, {NewLng})",
                    location.LocationCode,
                    location.LocationName,
                    oldLatitude?.ToString() ?? "null",
                    oldLongitude?.ToString() ?? "null",
                    request.Latitude,
                    request.Longitude);

                return new UpdateCustomerLocationGpsResult
                {
                    Success = true,
                    LocationId = location.Id,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    LocationName = location.LocationName,
                    Address = location.Address
                };
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                logger.LogError(ex, "Error updating GPS for location {LocationId}", request.LocationId);
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update GPS coordinates for location {LocationId}", request.LocationId);
            return new UpdateCustomerLocationGpsResult
            {
                Success = false,
                ErrorMessage = $"Failed to update GPS: {ex.Message}"
            };
        }
    }
}