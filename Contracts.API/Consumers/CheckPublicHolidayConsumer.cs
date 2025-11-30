using BuildingBlocks.Messaging.Events;

namespace Contracts.API.Consumers;

/// <summary>
/// Consumer kiểm tra xem một ngày có phải là ngày lễ không
/// Được gọi bởi Shifts.API khi generate shifts
/// </summary>
public class CheckPublicHolidayConsumer : IConsumer<CheckPublicHolidayRequest>
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<CheckPublicHolidayConsumer> _logger;

    public CheckPublicHolidayConsumer(
        IDbConnectionFactory connectionFactory,
        ILogger<CheckPublicHolidayConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CheckPublicHolidayRequest> context)
    {
        try
        {
            _logger.LogInformation(
                "Checking if {Date:yyyy-MM-dd} is a public holiday",
                context.Message.Date);

            using var connection = await _connectionFactory.CreateConnectionAsync();

            // ================================================================
            // KIỂM TRA NGÀY LỄ TRONG DATABASE
            // ================================================================
            var holiday = await connection.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT
                    HolidayName,
                    HolidayCategory,
                    IsTetPeriod
                  FROM public_holidays
                  WHERE HolidayDate = @Date
                    AND IsDeleted = 0",
                new { Date = context.Message.Date.Date });

            var response = new CheckPublicHolidayResponse
            {
                IsHoliday = holiday != null,
                HolidayName = holiday?.HolidayName,
                HolidayCategory = holiday?.HolidayCategory,
                IsTetPeriod = holiday?.IsTetPeriod ?? false
            };

            await context.RespondAsync(response);

            if (response.IsHoliday)
            {
                _logger.LogInformation(
                    "Date {Date:yyyy-MM-dd} is a public holiday: {HolidayName}",
                    context.Message.Date,
                    response.HolidayName);
            }
            else
            {
                _logger.LogDebug(
                    "Date {Date:yyyy-MM-dd} is not a public holiday",
                    context.Message.Date);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking public holiday for date {Date:yyyy-MM-dd}",
                context.Message.Date);

            // Respond with safe default (not a holiday)
            await context.RespondAsync(new CheckPublicHolidayResponse
            {
                IsHoliday = false,
                HolidayName = null,
                HolidayCategory = null,
                IsTetPeriod = false
            });
        }
    }
}
