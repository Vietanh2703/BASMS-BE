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
            "📨 Received ShiftAssignmentCreatedEvent: ShiftAssignment={AssignmentId}, Guard={GuardId}, Shift={ShiftId}",
            message.ShiftAssignmentId,
            message.GuardId,
            message.ShiftId);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            // ================================================================
            // KIỂM TRA XEM ĐÃ TỒN TẠI ATTENDANCE RECORD CHƯA
            // ================================================================
            // Note: Không query shift_assignments vì nó thuộc Shifts.API database
            // Nếu có cancellation, ShiftAssignmentCancelledEvent sẽ xử lý
            var existingRecord = await connection.QueryFirstOrDefaultAsync<AttendanceRecords>(
                @"SELECT * FROM attendance_records
                  WHERE ShiftAssignmentId = @ShiftAssignmentId
                    AND IsDeleted = 0",
                new { message.ShiftAssignmentId });

            if (existingRecord != null)
            {
                _logger.LogWarning(
                    "⚠️ Attendance record already exists for ShiftAssignment={AssignmentId}, skipping creation",
                    message.ShiftAssignmentId);
                return;
            }

            // ================================================================
            // TẠO ATTENDANCE RECORD MỚI
            // ================================================================
            var now = DateTimeHelper.VietnamNow;

            var attendanceRecord = new AttendanceRecords
            {
                Id = Guid.NewGuid(),
                ShiftAssignmentId = message.ShiftAssignmentId,
                GuardId = message.GuardId,
                ShiftId = message.ShiftId,

                // Thời gian dự kiến (từ shift)
                ScheduledStartTime = message.ScheduledStartTime,
                ScheduledEndTime = message.ScheduledEndTime,

                // Status: PENDING (chờ guard check-in)
                Status = "PENDING",
                IsIncomplete = true,            // Chưa có check-in/out
                IsVerified = false,
                VerificationStatus = "PENDING",

                // Break time mặc định
                BreakDurationMinutes = 60,

                // Audit
                CreatedAt = now
            };

            // Insert vào database
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
                "✅ Created attendance record {RecordId} for ShiftAssignment={AssignmentId}, Guard={GuardId}",
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
                "❌ Error creating attendance record for ShiftAssignment={AssignmentId}",
                message.ShiftAssignmentId);

            // Throw để MassTransit retry
            throw;
        }
    }
}
