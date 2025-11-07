namespace Contracts.API.ContractsHandler.CheckLocationClosed;

// Query để check location có đóng cửa không
public record CheckLocationClosedQuery(Guid LocationId, DateTime Date) : IQuery<CheckLocationClosedResult>;

// Result
public record CheckLocationClosedResult(
    bool IsClosed,
    string? Reason,
    string? DayType
);

internal class CheckLocationClosedHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<CheckLocationClosedHandler> logger)
    : IQueryHandler<CheckLocationClosedQuery, CheckLocationClosedResult>
{
    public async Task<CheckLocationClosedResult> Handle(
        CheckLocationClosedQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Checking if location {LocationId} is closed on {Date:yyyy-MM-dd}",
                request.LocationId,
                request.Date);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // Check location_special_days
            var sql = @"SELECT * FROM location_special_days
                       WHERE LocationId = @LocationId
                       AND SpecialDayDate = @Date";

            var specialDay = await connection.QueryFirstOrDefaultAsync<LocationSpecialDay>(
                sql,
                new { request.LocationId, Date = request.Date.Date });

            if (specialDay == null)
            {
                // Không có special day -> location mở cửa bình thường
                return new CheckLocationClosedResult(
                    IsClosed: false,
                    Reason: null,
                    DayType: null
                );
            }

            // Check if location operating on this special day
            if (specialDay.IsOperating)
            {
                return new CheckLocationClosedResult(
                    IsClosed: false,
                    Reason: specialDay.Reason,
                    DayType: specialDay.DayType
                );
            }

            logger.LogInformation(
                "Location {LocationId} is closed on {Date:yyyy-MM-dd}: {Reason}",
                request.LocationId,
                request.Date,
                specialDay.Reason);

            return new CheckLocationClosedResult(
                IsClosed: true,
                Reason: specialDay.Reason,
                DayType: specialDay.DayType
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error checking if location {LocationId} is closed on {Date}",
                request.LocationId,
                request.Date);
            throw;
        }
    }
}
