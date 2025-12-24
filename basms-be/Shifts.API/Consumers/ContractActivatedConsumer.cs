using Shifts.API.ShiftsHandler.ImportShiftTemplates;

namespace Shifts.API.Consumers;

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
                @"Contract {ContractNumber} activated successfully!
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


            _logger.LogInformation(
                "Importing {Count} shift templates from Contract {ContractNumber}",
                @event.ShiftSchedules.Count,
                @event.ContractNumber);

            var importCommand = new ImportShiftTemplatesCommand(
                ContractId: @event.ContractId,
                ContractNumber: @event.ContractNumber,
                ShiftSchedules: @event.ShiftSchedules,
                Locations: @event.Locations,
                ManagerId: @event.ManagerId,
                ImportedBy: @event.ActivatedBy
            );

            var importResult = await _sender.Send(importCommand);

            _logger.LogInformation(
                @"Template import completed for Contract {ContractNumber}:
                  - Created: {Created} templates
                  - Updated: {Updated} templates
                  - Skipped: {Skipped} templates
                  - Errors: {Errors}",
                @event.ContractNumber,
                importResult.TemplatesCreatedCount,
                importResult.TemplatesUpdatedCount,
                importResult.TemplatesSkippedCount,
                importResult.Errors.Count);

            if (importResult.Errors.Any())
            {
                _logger.LogWarning("Template import errors:");
                foreach (var error in importResult.Errors)
                {
                    _logger.LogWarning("  - {Error}", error);
                }
            }
            
            _logger.LogInformation(
                "Contract {ContractNumber} activated. " +
                "Manager can now create shifts" +
                "with ManagerId and ShiftTemplateId from imported templates.",
                @event.ContractNumber);
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process ContractActivatedEvent for Contract {ContractNumber}",
                @event.ContractNumber);

            throw;
        }
    }
}
