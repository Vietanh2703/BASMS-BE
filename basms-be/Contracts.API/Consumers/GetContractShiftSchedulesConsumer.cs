using BuildingBlocks.Messaging.Events;
using Dapper;

namespace Contracts.API.Consumers;

/// <summary>
/// Consumer xử lý request lấy shift schedules từ contract
/// Được gọi bởi Shifts.API khi generate shifts
/// </summary>
public class GetContractShiftSchedulesConsumer : IConsumer<GetContractShiftSchedulesRequest>
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<GetContractShiftSchedulesConsumer> _logger;

    public GetContractShiftSchedulesConsumer(
        IDbConnectionFactory connectionFactory,
        ILogger<GetContractShiftSchedulesConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetContractShiftSchedulesRequest> context)
    {
        try
        {
            _logger.LogInformation(
                "Received GetContractShiftSchedulesRequest for Contract {ContractId}",
                context.Message.ContractId);

            using var connection = await _connectionFactory.CreateConnectionAsync();

            // ================================================================
            // 1. LẤY THÔNG TIN CONTRACT
            // ================================================================
            var contract = await connection.QueryFirstOrDefaultAsync<Models.Contract>(
                @"SELECT Id, ContractNumber, StartDate, EndDate, WorkOnPublicHolidays
                  FROM contracts
                  WHERE Id = @ContractId AND IsDeleted = 0",
                new { ContractId = context.Message.ContractId });

            if (contract == null)
            {
                _logger.LogWarning(
                    "Contract {ContractId} not found",
                    context.Message.ContractId);

                // Respond with empty result
                await context.RespondAsync(new GetContractShiftSchedulesResponse
                {
                    ContractId = context.Message.ContractId,
                    ContractNumber = string.Empty,
                    Schedules = new List<ShiftScheduleInfo>(),
                    Locations = new List<BuildingBlocks.Messaging.Events.LocationInfo>()
                });
                return;
            }

            // ================================================================
            // 2. LẤY SHIFT SCHEDULES
            // ================================================================
            var schedules = (await connection.QueryAsync<dynamic>(
                @"SELECT
                    Id as ScheduleId,
                    ScheduleName,
                    ScheduleType,
                    LocationId,
                    ShiftStartTime,
                    ShiftEndTime,
                    CrossesMidnight,
                    BreakMinutes,
                    GuardsPerShift,
                    AppliesMonday,
                    AppliesTuesday,
                    AppliesWednesday,
                    AppliesThursday,
                    AppliesFriday,
                    AppliesSaturday,
                    AppliesSunday,
                    AppliesOnPublicHolidays,
                    AppliesOnWeekends,
                    SkipWhenLocationClosed,
                    RequiresArmedGuard,
                    RequiresSupervisor,
                    EffectiveFrom,
                    EffectiveTo
                  FROM contract_shift_schedules
                  WHERE ContractId = @ContractId
                    AND IsDeleted = 0
                    AND IsActive = 1
                  ORDER BY ScheduleName",
                new { ContractId = contract.Id }
            )).ToList();

            _logger.LogInformation(
                "Found {ScheduleCount} shift schedules for contract {ContractId}",
                schedules.Count,
                contract.Id);

            // ================================================================
            // 3. LẤY LOCATIONS VỚI THÔNG TIN ĐẦY ĐỦ
            // ================================================================
            var locations = (await connection.QueryAsync<dynamic>(
                @"SELECT
                    cl.LocationId as LocationId,
                    loc.LocationName,
                    loc.LocationCode,
                    loc.Address,
                    loc.Latitude,
                    loc.Longitude,
                    cl.GuardsRequired
                  FROM contract_locations cl
                  INNER JOIN customer_locations loc ON cl.LocationId = loc.Id
                  WHERE cl.ContractId = @ContractId
                    AND cl.IsDeleted = 0
                    AND loc.IsDeleted = 0",
                new { ContractId = contract.Id }
            )).ToList();

            _logger.LogInformation(
                "Found {LocationCount} locations for contract {ContractId}",
                locations.Count,
                contract.Id);

            // ================================================================
            // 4. MAP DATA VÀ RESPOND
            // ================================================================
            var response = new GetContractShiftSchedulesResponse
            {
                ContractId = contract.Id,
                ContractNumber = contract.ContractNumber,
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                WorkOnPublicHolidays = contract.WorkOnPublicHolidays,
                Schedules = schedules.Select(s => new ShiftScheduleInfo
                {
                    ScheduleId = s.ScheduleId,
                    ScheduleName = s.ScheduleName ?? "Unknown",
                    ScheduleType = s.ScheduleType ?? "REGULAR",
                    LocationId = s.LocationId,
                    ShiftStartTime = s.ShiftStartTime,
                    ShiftEndTime = s.ShiftEndTime,
                    CrossesMidnight = s.CrossesMidnight ?? false,
                    BreakMinutes = s.BreakMinutes ?? 60,
                    GuardsPerShift = s.GuardsPerShift ?? 1,
                    AppliesMonday = s.AppliesMonday ?? true,
                    AppliesTuesday = s.AppliesTuesday ?? true,
                    AppliesWednesday = s.AppliesWednesday ?? true,
                    AppliesThursday = s.AppliesThursday ?? true,
                    AppliesFriday = s.AppliesFriday ?? true,
                    AppliesSaturday = s.AppliesSaturday ?? false,
                    AppliesSunday = s.AppliesSunday ?? false,
                    AppliesOnPublicHolidays = s.AppliesOnPublicHolidays ?? true,
                    AppliesOnWeekends = s.AppliesOnWeekends ?? false,
                    SkipWhenLocationClosed = s.SkipWhenLocationClosed ?? true,
                    RequiresArmedGuard = s.RequiresArmedGuard ?? false,
                    RequiresSupervisor = s.RequiresSupervisor ?? false,
                    EffectiveFrom = s.EffectiveFrom,
                    EffectiveTo = s.EffectiveTo
                }).ToList(),
                Locations = locations.Select(l => new BuildingBlocks.Messaging.Events.LocationInfo
                {
                    LocationId = l.LocationId,
                    LocationName = l.LocationName ?? "Unknown",
                    LocationCode = l.LocationCode ?? string.Empty,
                    Address = l.Address,
                    Latitude = l.Latitude,
                    Longitude = l.Longitude,
                    GuardsRequired = l.GuardsRequired ?? 1
                }).ToList()
            };

            await context.RespondAsync(response);

            _logger.LogInformation(
                "Responded with {ScheduleCount} schedules and {LocationCount} locations for contract {ContractNumber}",
                response.Schedules.Count,
                response.Locations.Count,
                contract.ContractNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling GetContractShiftSchedulesRequest for Contract {ContractId}",
                context.Message.ContractId);

            // Respond with error
            await context.RespondAsync(new GetContractShiftSchedulesResponse
            {
                ContractId = context.Message.ContractId,
                ContractNumber = string.Empty,
                Schedules = new List<ShiftScheduleInfo>(),
                Locations = new List<BuildingBlocks.Messaging.Events.LocationInfo>()
            });
        }
    }
}