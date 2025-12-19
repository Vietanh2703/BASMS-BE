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
            "Received GetShiftLocationRequest for ShiftId: {ShiftId}",
            request.ShiftId);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            var shift = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT
                    Id,
                    LocationLatitude,
                    LocationLongitude,
                    ScheduledStartTime
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
                    LocationLatitude = (double)shift.LocationLatitude,
                    LocationLongitude = (double)shift.LocationLongitude,
                    ScheduledStartTime = (DateTime)shift.ScheduledStartTime
                },
                ErrorMessage = null
            };

            _logger.LogInformation(
                "Returning shift location info for ShiftId: {ShiftId} at ({Lat}, {Lon})",
                request.ShiftId,
                response.Location.LocationLatitude,
                response.Location.LocationLongitude);

            await context.RespondAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get shift location for ShiftId: {ShiftId}",
                request.ShiftId);

            throw; // Re-throw to trigger MassTransit retry
        }
    }
}
