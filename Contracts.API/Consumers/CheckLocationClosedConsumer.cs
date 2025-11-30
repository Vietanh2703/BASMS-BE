using BuildingBlocks.Messaging.Events;

namespace Contracts.API.Consumers;

/// <summary>
/// Consumer kiểm tra xem location có đóng cửa vào ngày cụ thể không
/// Được gọi bởi Shifts.API khi generate shifts
/// </summary>
public class CheckLocationClosedConsumer : IConsumer<CheckLocationClosedRequest>
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<CheckLocationClosedConsumer> _logger;

    public CheckLocationClosedConsumer(
        IDbConnectionFactory connectionFactory,
        ILogger<CheckLocationClosedConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CheckLocationClosedRequest> context)
    {
        try
        {
            _logger.LogInformation(
                "Checking if location {LocationId} is closed on {Date:yyyy-MM-dd}",
                context.Message.LocationId,
                context.Message.Date);

            using var connection = await _connectionFactory.CreateConnectionAsync();

            // ================================================================
            // KIỂM TRA LOCATION SPECIAL DAYS (ĐÓNG CỬA)
            // ================================================================
            var specialDay = await connection.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT
                    DayType,
                    Reason,
                    IsClosed
                  FROM location_special_days
                  WHERE LocationId = @LocationId
                    AND SpecialDate = @Date
                    AND IsDeleted = 0",
                new
                {
                    LocationId = context.Message.LocationId,
                    Date = context.Message.Date.Date
                });

            bool isClosed = false;
            string? reason = null;
            string? dayType = null;

            if (specialDay != null)
            {
                isClosed = specialDay.IsClosed ?? false;
                reason = specialDay.Reason;
                dayType = specialDay.DayType;
            }
            else
            {
                // Kiểm tra operating schedule (lịch mở cửa thường xuyên)
                var dayOfWeek = (int)context.Message.Date.DayOfWeek; // 0=Sunday, 1=Monday...

                var operating = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT
                        IsMondayOpen,
                        IsTuesdayOpen,
                        IsWednesdayOpen,
                        IsThursdayOpen,
                        IsFridayOpen,
                        IsSaturdayOpen,
                        IsSundayOpen
                      FROM location_operating_schedules
                      WHERE LocationId = @LocationId
                        AND IsActive = 1
                      LIMIT 1",
                    new { LocationId = context.Message.LocationId });

                if (operating != null)
                {
                    // Kiểm tra theo ngày trong tuần
                    bool isOpen = dayOfWeek switch
                    {
                        0 => operating.IsSundayOpen ?? true,
                        1 => operating.IsMondayOpen ?? true,
                        2 => operating.IsTuesdayOpen ?? true,
                        3 => operating.IsWednesdayOpen ?? true,
                        4 => operating.IsThursdayOpen ?? true,
                        5 => operating.IsFridayOpen ?? true,
                        6 => operating.IsSaturdayOpen ?? true,
                        _ => true
                    };

                    if (!isOpen)
                    {
                        isClosed = true;
                        reason = $"Location normally closed on {context.Message.Date.DayOfWeek}";
                        dayType = "REGULAR_CLOSED_DAY";
                    }
                }
            }

            var response = new CheckLocationClosedResponse
            {
                IsClosed = isClosed,
                Reason = reason,
                DayType = dayType
            };

            await context.RespondAsync(response);

            if (response.IsClosed)
            {
                _logger.LogInformation(
                    "Location {LocationId} is closed on {Date:yyyy-MM-dd}: {Reason}",
                    context.Message.LocationId,
                    context.Message.Date,
                    response.Reason);
            }
            else
            {
                _logger.LogDebug(
                    "Location {LocationId} is open on {Date:yyyy-MM-dd}",
                    context.Message.LocationId,
                    context.Message.Date);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking if location {LocationId} is closed on {Date:yyyy-MM-dd}",
                context.Message.LocationId,
                context.Message.Date);

            // Respond with safe default (not closed)
            await context.RespondAsync(new CheckLocationClosedResponse
            {
                IsClosed = false,
                Reason = null,
                DayType = null
            });
        }
    }
}
