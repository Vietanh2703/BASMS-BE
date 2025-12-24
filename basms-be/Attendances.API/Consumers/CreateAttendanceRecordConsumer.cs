namespace Attendances.API.Consumers;

public class CreateAttendanceRecordConsumer : IConsumer<ShiftAssignmentCreatedEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<CreateAttendanceRecordConsumer> _logger;

    public CreateAttendanceRecordConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<CreateAttendanceRecordConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ShiftAssignmentCreatedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received ShiftAssignmentCreatedEvent: ShiftAssignment={AssignmentId}, Guard={GuardId}, Shift={ShiftId}",
            message.ShiftAssignmentId,
            message.GuardId,
            message.ShiftId);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();
            
            var existingRecord = await connection.QueryFirstOrDefaultAsync<AttendanceRecords>(
                @"SELECT * FROM attendance_records
                  WHERE ShiftAssignmentId = @ShiftAssignmentId
                    AND IsDeleted = 0",
                new { message.ShiftAssignmentId });

            if (existingRecord != null)
            {
                _logger.LogWarning(
                    "Attendance record already exists for ShiftAssignment={AssignmentId}, skipping creation",
                    message.ShiftAssignmentId);
                return;
            }
            
            var now = DateTimeHelper.VietnamNow;

            var attendanceRecord = new AttendanceRecords
            {
                Id = Guid.NewGuid(),
                ShiftAssignmentId = message.ShiftAssignmentId,
                GuardId = message.GuardId,
                ShiftId = message.ShiftId,
                ScheduledStartTime = message.ScheduledStartTime,
                ScheduledEndTime = message.ScheduledEndTime,
                Status = "PENDING",
                IsIncomplete = true,        
                IsVerified = false,
                VerificationStatus = "PENDING",
                BreakDurationMinutes = 60,
                CreatedAt = now
            };
            
            var insertSql = @"
                INSERT INTO attendance_records (
                    Id, ShiftAssignmentId, GuardId, ShiftId,
                    ScheduledStartTime, ScheduledEndTime,
                    Status, IsIncomplete, IsVerified, VerificationStatus,
                    BreakDurationMinutes, CreatedAt
                ) VALUES (
                    @Id, @ShiftAssignmentId, @GuardId, @ShiftId,
                    @ScheduledStartTime, @ScheduledEndTime,
                    @Status, @IsIncomplete, @IsVerified, @VerificationStatus,
                    @BreakDurationMinutes, @CreatedAt
                )";

            await connection.ExecuteAsync(insertSql, attendanceRecord);

            _logger.LogInformation(
                "Created attendance record {RecordId} for ShiftAssignment={AssignmentId}, Guard={GuardId}",
                attendanceRecord.Id,
                message.ShiftAssignmentId,
                message.GuardId);

            _logger.LogInformation(
                "   Scheduled: {Start} → {End}",
                message.ScheduledStartTime.ToString("yyyy-MM-dd HH:mm"),
                message.ScheduledEndTime.ToString("yyyy-MM-dd HH:mm"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error creating attendance record for ShiftAssignment={AssignmentId}",
                message.ShiftAssignmentId);
            
            throw;
        }
    }
}
