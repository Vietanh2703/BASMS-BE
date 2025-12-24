namespace Contracts.API.Consumers;

public class BatchCheckPublicHolidaysConsumer : IConsumer<BatchCheckPublicHolidaysRequest>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<BatchCheckPublicHolidaysConsumer> _logger;

    public BatchCheckPublicHolidaysConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<BatchCheckPublicHolidaysConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<BatchCheckPublicHolidaysRequest> context)
    {
        var request = context.Message;

        _logger.LogInformation(
            "Received BatchCheckPublicHolidaysRequest for {Count} dates",
            request.Dates.Count);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            var holidays = await connection.QueryAsync<dynamic>(@"
                SELECT
                    HolidayDate,
                    HolidayName,
                    HolidayNameEn,
                    HolidayCategory,
                    IsTetPeriod,
                    IsTetHoliday,
                    TetDayNumber
                FROM public_holidays
                WHERE HolidayDate IN @Dates
                  AND IsOfficialHoliday = 1
                  AND IsObserved = 1
                ORDER BY HolidayDate",
                new { Dates = request.Dates });

            var holidayDict = holidays
                .ToDictionary(
                    h => ((DateTime)h.HolidayDate).Date,
                    h => new HolidayInfo
                    {
                        HolidayName = h.HolidayName,
                        HolidayNameEn = h.HolidayNameEn,
                        HolidayCategory = h.HolidayCategory,
                        IsTetPeriod = h.IsTetPeriod == 1,
                        IsTetHoliday = h.IsTetHoliday == 1,
                        TetDayNumber = h.TetDayNumber
                    });

            var response = new Dictionary<DateTime, HolidayInfo?>();

            foreach (var date in request.Dates)
            {
                var normalizedDate = date.Date;
                response[normalizedDate] = holidayDict.GetValueOrDefault(normalizedDate);
            }

            _logger.LogInformation(
                "Batch holiday check completed: {TotalDates} dates checked, {HolidayCount} holidays found",
                request.Dates.Count,
                holidayDict.Count);

            await context.RespondAsync(new BatchCheckPublicHolidaysResponse
            {
                Holidays = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process BatchCheckPublicHolidaysRequest");
            throw;
        }
    }
}
