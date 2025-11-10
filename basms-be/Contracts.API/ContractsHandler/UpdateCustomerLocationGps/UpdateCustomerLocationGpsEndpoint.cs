namespace Contracts.API.ContractsHandler.UpdateCustomerLocationGps;

public class UpdateCustomerLocationGpsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: PUT /api/locations/{locationId}/gps
        app.MapPut("/api/locations/{locationId:guid}/gps",
            async (Guid locationId, UpdateGpsRequest request, ISender sender) =>
            {
                var command = new UpdateCustomerLocationGpsCommand(
                    locationId,
                    request.Latitude,
                    request.Longitude
                );

                var result = await sender.Send(command);

                if (!result.Success)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = result.ErrorMessage
                    });
                }

                return Results.Ok(new
                {
                    success = true,
                    data = new
                    {
                        locationId = result.LocationId,
                        latitude = result.Latitude,
                        longitude = result.Longitude,
                        locationName = result.LocationName,
                        address = result.Address
                    }
                });
            })
        .WithTags("Locations")
        .WithName("UpdateLocationGps")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Update location GPS coordinates")
        .WithDescription("Update only latitude and longitude of a location. Does not allow changing other location information.");
    }
}

/// <summary>
/// Request body cho update GPS
/// </summary>
public record UpdateGpsRequest(
    decimal Latitude,
    decimal Longitude
);