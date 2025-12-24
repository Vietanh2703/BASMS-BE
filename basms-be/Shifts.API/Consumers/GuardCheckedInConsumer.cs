namespace Shifts.API.Consumers;

public class GuardCheckedInConsumer : IConsumer<GuardCheckedInEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<GuardCheckedInConsumer> _logger;

    public GuardCheckedInConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<GuardCheckedInConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GuardCheckedInEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received GuardCheckedInEvent: Guard={GuardId}, ShiftAssignment={AssignmentId}, Shift={ShiftId}, CheckInTime={CheckInTime}",
            message.GuardId,
            message.ShiftAssignmentId,
            message.ShiftId,
            message.CheckInTime.ToString("yyyy-MM-dd HH:mm:ss"));

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();
            var updateAssignmentSql = @"
                UPDATE shift_assignments
                SET ConfirmedAt = @ConfirmedAt,
                    CheckedInAt = @CheckedInAt,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @ShiftAssignmentId";

            var assignmentUpdated = await connection.ExecuteAsync(updateAssignmentSql, new
            {
                ShiftAssignmentId = message.ShiftAssignmentId,
                ConfirmedAt = message.ConfirmedAt,
                CheckedInAt = message.CheckInTime,
                UpdatedAt = DateTime.UtcNow
            });

            if (assignmentUpdated > 0)
            {
                _logger.LogInformation(
                    "✓ Updated ShiftAssignment={AssignmentId}: ConfirmedAt={ConfirmedAt}, CheckedInAt={CheckedInAt}",
                    message.ShiftAssignmentId,
                    message.ConfirmedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    message.CheckInTime.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else
            {
                _logger.LogWarning(
                    "ShiftAssignment not found or not updated: {AssignmentId}",
                    message.ShiftAssignmentId);
            }
            
            var isFirstCheckIn = assignmentUpdated > 0;

            if (isFirstCheckIn)
            {
                var updateShiftSql = @"
                    UPDATE shifts
                    SET ConfirmedGuardsCount = COALESCE(ConfirmedGuardsCount, 0) + 1,
                        CheckedInGuardsCount = COALESCE(CheckedInGuardsCount, 0) + 1,
                        UpdatedAt = @UpdatedAt
                    WHERE Id = @ShiftId";

                var shiftUpdated = await connection.ExecuteAsync(updateShiftSql, new
                {
                    ShiftId = message.ShiftId,
                    UpdatedAt = DateTime.UtcNow
                });

                if (shiftUpdated > 0)
                {
                    _logger.LogInformation(
                        "✓ Updated Shift={ShiftId}: ConfirmedGuardsCount+1, CheckedInGuardsCount+1",
                        message.ShiftId);
                    
                    var shiftStats = await connection.QueryFirstOrDefaultAsync(
                        @"SELECT ConfirmedGuardsCount, CheckedInGuardsCount, RequiredGuardsCount
                          FROM shifts
                          WHERE Id = @ShiftId",
                        new { ShiftId = message.ShiftId });

                    if (shiftStats != null)
                    {
                        _logger.LogInformation(
                            "   Shift stats: Checked-in={CheckedIn}/{Required}, Confirmed={Confirmed}",
                            (string)shiftStats.CheckedInGuardsCount,
                            (string)shiftStats.RequiredGuardsCount,
                            (string)shiftStats.ConfirmedGuardsCount);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Shift not found or not updated: {ShiftId}",
                        message.ShiftId);
                }
            }

            _logger.LogInformation(
                "GuardCheckedInEvent processed successfully: Guard={GuardId}, Late={IsLate} ({LateMinutes}min), FaceMatch={FaceMatchScore}%, Distance={Distance}m",
                message.GuardId,
                message.IsLate,
                message.LateMinutes,
                message.FaceMatchScore,
                Math.Round(message.DistanceFromSite, 2));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing GuardCheckedInEvent for Guard={GuardId}, ShiftAssignment={AssignmentId}",
                message.GuardId,
                message.ShiftAssignmentId);
            throw;
        }
    }
}
