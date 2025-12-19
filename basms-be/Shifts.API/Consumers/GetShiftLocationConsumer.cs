using BuildingBlocks.Messaging.Events;
using Dapper;

namespace Shifts.API.Consumers;

/// <summary>
/// Consumer trả về thông tin location của shift
/// Used by: Attendances.API khi guard check-in để validate location
/// </summary>
public class GetShiftLocationConsumer : IConsumer<GetShiftLocationRequest>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<GetShiftLocationConsumer> _logger;

    public GetShiftLocationConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<GetShiftLocationConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetShiftLocationRequest> context)
    {
        var request = context.Message;

        _logger.LogInformation(
            "🔵 [GetShiftLocationConsumer] Received request for ShiftId: {ShiftId}",
            request.ShiftId);

        try
        {
            _logger.LogInformation("🔵 [GetShiftLocationConsumer] Creating database connection...");
            using var connection = await _dbFactory.CreateConnectionAsync();
            _logger.LogInformation("🔵 [GetShiftLocationConsumer] Database connection created successfully");

            var shift = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT
                    Id,
                    LocationLatitude,
                    LocationLongitude,
                    ShiftStart,
                    ShiftEnd
                FROM shifts
                WHERE Id = @ShiftId
                  AND IsDeleted = 0",
                new { request.ShiftId });

            if (shift == null)
            {
                _logger.LogWarning(
                    "Shift {ShiftId} not found",
                    request.ShiftId);

                // Return error response
                await context.RespondAsync(new GetShiftLocationResponse
                {
                    Success = false,
                    Location = null,
                    ErrorMessage = "Shift không tồn tại hoặc đã bị xóa"
                });
                return;
            }

            var response = new GetShiftLocationResponse
            {
                Success = true,
                Location = new ShiftLocationData
                {
                    ShiftId = (Guid)shift.Id,
                    LocationLatitude = shift.LocationLatitude != null ? (double)(decimal)shift.LocationLatitude : 0.0,
                    LocationLongitude = shift.LocationLongitude != null ? (double)(decimal)shift.LocationLongitude : 0.0,
                    ScheduledStartTime = (DateTime)shift.ShiftStart,
                    ScheduledEndTime = (DateTime)shift.ShiftEnd
                },
                ErrorMessage = null
            };

            _logger.LogInformation(
                "✅ [GetShiftLocationConsumer] Returning shift location for ShiftId: {ShiftId} at ({Lat}, {Lon})",
                request.ShiftId,
                response.Location.LocationLatitude,
                response.Location.LocationLongitude);

            await context.RespondAsync(response);
            _logger.LogInformation("✅ [GetShiftLocationConsumer] Response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get shift location for ShiftId: {ShiftId}",
                request.ShiftId);

            // Send error response instead of throwing to avoid timeout on requester side
            await context.RespondAsync(new GetShiftLocationResponse
            {
                Success = false,
                Location = null,
                ErrorMessage = $"Error retrieving shift location: {ex.Message}"
            });
        }
    }
}
