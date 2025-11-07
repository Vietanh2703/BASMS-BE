using BuildingBlocks.Messaging.Events;

namespace Shifts.API.Consumers;

/// <summary>
/// Consumer nhận ContractActivatedEvent từ Contracts Service
/// WORKFLOW:
/// 1. Contract được activate trong Contracts.API
/// 2. Event được publish qua RabbitMQ
/// 3. Shifts.API nhận event và lưu thông tin contract
/// 4. Thông báo manager có thể bắt đầu tạo shifts cho contract này
///
/// NOTE: Consumer này chỉ lưu metadata, không tự động tạo shifts
/// Shifts sẽ được tạo sau bởi background job hoặc manual trigger
/// </summary>
public class ContractActivatedConsumer : IConsumer<ContractActivatedEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<ContractActivatedConsumer> _logger;

    public ContractActivatedConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<ContractActivatedConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
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

            // TODOLưu contract metadata vào Shifts database nếu cần
            // Hiện tại chỉ log event để manager biết có contract mới

            _logger.LogInformation(
                @"✓ Contract {ContractNumber} activated successfully!
                  - Customer: {CustomerName}
                  - Start Date: {StartDate:yyyy-MM-dd}
                  - End Date: {EndDate:yyyy-MM-dd}
                  - Locations: {LocationCount}
                  - Shift Schedules: {ScheduleCount}
                  - Auto Generate Shifts: {AutoGenerate}

                  ➜ Manager can now create shifts for this contract.",
                @event.ContractNumber,
                @event.CustomerName,
                @event.StartDate,
                @event.EndDate,
                @event.Locations.Count,
                @event.ShiftSchedules.Count,
                @event.AutoGenerateShifts ? "YES" : "NO");

            // Nếu AutoGenerateShifts = true, trigger background job
            // để tự động tạo shifts theo schedules
            if (@event.AutoGenerateShifts)
            {
                _logger.LogInformation(
                    "Contract {ContractNumber} has AutoGenerateShifts enabled. " +
                    "Background job should be triggered to create shifts for the next {Days} days.",
                    @event.ContractNumber,
                    @event.GenerateShiftsAdvanceDays);

                // Implement background job trigger here
                // await _shiftGenerationService.TriggerShiftGenerationAsync(@event.ContractId);
            }

            // Send notification to managers
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
