namespace Contracts.API.ContractsHandler.CheckPublicHoliday;

public record CheckPublicHolidayQuery(DateTime Date) : IQuery<CheckPublicHolidayResult>;

public record CheckPublicHolidayResult(
    bool IsHoliday,
    string? HolidayName,
    string? HolidayCategory,
    bool IsTetPeriod
);

internal class CheckPublicHolidayHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<CheckPublicHolidayHandler> logger)
    : IQueryHandler<CheckPublicHolidayQuery, CheckPublicHolidayResult>
{
    public async Task<CheckPublicHolidayResult> Handle(
        CheckPublicHolidayQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Checking if {Date:yyyy-MM-dd} is public holiday", request.Date);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // Query public_holidays table
            var sql = @"SELECT * FROM public_holidays
                       WHERE HolidayDate = @Date
                       AND IsObserved = 1";

            var holiday = await connection.QueryFirstOrDefaultAsync<PublicHoliday>(
                sql,
                new { Date = request.Date.Date });

            if (holiday == null)
            {
                logger.LogInformation("{Date:yyyy-MM-dd} is not a public holiday", request.Date);

                return new CheckPublicHolidayResult(
                    IsHoliday: false,
                    HolidayName: null,
                    HolidayCategory: null,
                    IsTetPeriod: false
                );
            }

            logger.LogInformation(
                "{Date:yyyy-MM-dd} is {HolidayName}",
                request.Date,
                holiday.HolidayName);

            return new CheckPublicHolidayResult(
                IsHoliday: true,
                HolidayName: holiday.HolidayName,
                HolidayCategory: holiday.HolidayCategory,
                IsTetPeriod: holiday.IsTetPeriod
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking holiday for {Date}", request.Date);
            throw;
        }
    }
}
