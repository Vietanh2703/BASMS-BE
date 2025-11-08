namespace Shifts.API.Consumers;

/// <summary>
/// Consumer nhận ContractActivatedEvent từ Contracts Service
/// WORKFLOW:
/// 1. Contract được activate trong Contracts.API
/// 2. Event được publish qua RabbitMQ
/// 3. Shifts.API nhận event
/// 4. Nếu AutoGenerateShifts = true → Tự động tạo ca làm
/// 5. Gửi thông báo cho managers về contract mới
/// </summary>
public class ContractActivatedConsumer : IConsumer<ContractActivatedEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<ContractActivatedConsumer> _logger;
    private readonly ISender _sender;

    public ContractActivatedConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<ContractActivatedConsumer> logger,
        ISender sender)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _sender = sender;
    }

    public async Task Consume(ConsumeContext<ContractActivatedEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "Received ContractActivatedEvent for Contract {ContractNumber} (ID: {ContractId})",
            @event.ContractNumber,
            @event.ContractId);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            _logger.LogInformation(
                @"✓ Contract {ContractNumber} activated successfully!
                  - Customer: {CustomerName}
                  - Start Date: {StartDate:yyyy-MM-dd}
                  - End Date: {EndDate:yyyy-MM-dd}
                  - Locations: {LocationCount}
                  - Shift Schedules: {ScheduleCount}
                  - Auto Generate Shifts: {AutoGenerate}",
                @event.ContractNumber,
                @event.CustomerName,
                @event.StartDate,
                @event.EndDate,
                @event.Locations.Count,
                @event.ShiftSchedules.Count,
                @event.AutoGenerateShifts ? "YES" : "NO");

            // ================================================================
            // TỰ ĐỘNG TẠO CA NẾU ĐƯỢC BẬT
            // ================================================================
            if (@event.AutoGenerateShifts)
            {
                _logger.LogInformation(
                    "Auto-generating shifts for Contract {ContractNumber} - {Days} days in advance",
                    @event.ContractNumber,
                    @event.GenerateShiftsAdvanceDays);

                var command = new GenerateShiftsCommand(
                    ContractId: @event.ContractId,
                    GenerateFromDate: DateTime.UtcNow.Date,
                    GenerateDays: @event.GenerateShiftsAdvanceDays,
                    CreatedBy: @event.ActivatedBy
                );

                var result = await _sender.Send(command);

                _logger.LogInformation(
                    @"✓ Shift generation completed for Contract {ContractNumber}:
                      - Shifts Created: {CreatedCount}
                      - Shifts Skipped: {SkippedCount}
                      - Errors: {ErrorCount}
                      - Date Range: {From:yyyy-MM-dd} to {To:yyyy-MM-dd}",
                    @event.ContractNumber,
                    result.ShiftsCreatedCount,
                    result.ShiftsSkippedCount,
                    result.Errors.Count,
                    result.GeneratedFrom,
                    result.GeneratedTo);

                if (result.SkipReasons.Any())
                {
                    _logger.LogInformation("Skip reasons summary:");
                    var groupedReasons = result.SkipReasons
                        .GroupBy(r => r.Reason)
                        .Select(g => new { Reason = g.Key, Count = g.Count() });

                    foreach (var reason in groupedReasons)
                    {
                        _logger.LogInformation("  - {Reason}: {Count} times", reason.Reason, reason.Count);
                    }
                }

                if (result.Errors.Any())
                {
                    _logger.LogWarning("Errors during shift generation:");
                    foreach (var error in result.Errors)
                    {
                        _logger.LogWarning("  - {Error}", error);
                    }
                }
            }
            else
            {
                _logger.LogInformation(
                    "Contract {ContractNumber} does not have auto-generate enabled. " +
                    "Manager can manually create shifts via API.",
                    @event.ContractNumber);
            }

            //Send notification to managers about new contract
            // await _notificationService.NotifyManagersAboutNewContract(@event);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process ContractActivatedEvent for Contract {ContractNumber}",
                @event.ContractNumber);

            throw; // Re-throw to trigger MassTransit retry
        }
    }
}
