using BuildingBlocks.Messaging.Events;

namespace Contracts.API.Consumers;

/// <summary>
/// Consumer nhận ShiftsGeneratedEvent từ Shifts.API
/// Update shift_generation_log để tracking và audit
/// </summary>
public class ShiftsGeneratedConsumer : IConsumer<ShiftsGeneratedEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<ShiftsGeneratedConsumer> _logger;

    public ShiftsGeneratedConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<ShiftsGeneratedConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ShiftsGeneratedEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "Received ShiftsGeneratedEvent for Contract {ContractNumber} - Date: {GenerationDate}",
            @event.ContractNumber,
            @event.GenerationDate);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            // Tạo log entry
            var log = new ShiftGenerationLog
            {
                Id = Guid.NewGuid(),
                ContractId = @event.ContractId,
                ContractShiftScheduleId = @event.ContractShiftScheduleId,
                GenerationDate = @event.GenerationDate,
                GeneratedAt = @event.GeneratedAt,
                ShiftsCreatedCount = @event.ShiftsCreatedCount,
                ShiftsSkippedCount = @event.ShiftsSkippedCount,
                SkipReasons = @event.SkipReasons,
                Status = @event.Status,
                ErrorMessage = @event.ErrorMessage,
                GeneratedByJob = @event.GeneratedByJob
            };

            await connection.InsertAsync(log);

            _logger.LogInformation(
                @"✓ Shift generation logged for Contract {ContractNumber}:
                  - Generation Date: {GenerationDate:yyyy-MM-dd}
                  - Created: {Created} shifts
                  - Skipped: {Skipped} shifts
                  - Status: {Status}
                  - Duration: {Duration}ms
                  - Job: {Job}",
                @event.ContractNumber,
                @event.GenerationDate,
                @event.ShiftsCreatedCount,
                @event.ShiftsSkippedCount,
                @event.Status,
                @event.GenerationDurationMs,
                @event.GeneratedByJob);

            // Log chi tiết nếu có shifts được tạo
            if (@event.GeneratedShifts.Any())
            {
                _logger.LogInformation(
                    "Generated shifts details: {Shifts}",
                    string.Join(", ", @event.GeneratedShifts.Select(s =>
                        $"{s.LocationName} {s.ShiftDate:yyyy-MM-dd} {s.ShiftStartTime}-{s.ShiftEndTime}")));
            }

            // Log skip reasons nếu có
            if (@event.ShiftsSkippedCount > 0 && !string.IsNullOrEmpty(@event.SkipReasons))
            {
                _logger.LogInformation(
                    "Skipped shifts reasons: {Reasons}",
                    @event.SkipReasons);
            }

            // Nếu failed, log error
            if (@event.Status == "failed" && !string.IsNullOrEmpty(@event.ErrorMessage))
            {
                _logger.LogError(
                    "Shift generation failed for Contract {ContractNumber}: {Error}",
                    @event.ContractNumber,
                    @event.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process ShiftsGeneratedEvent for Contract {ContractNumber}",
                @event.ContractNumber);

            throw; // Re-throw to trigger MassTransit retry
        }
    }
}
