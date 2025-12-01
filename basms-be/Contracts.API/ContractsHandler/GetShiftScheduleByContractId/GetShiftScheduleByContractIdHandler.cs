namespace Contracts.API.ContractsHandler.GetShiftScheduleByContractId;

// ================================================================
// QUERY & RESULT
// ================================================================

/// <summary>
/// Query để lấy danh sách shift schedules theo contract ID
/// </summary>
public record GetShiftScheduleByContractIdQuery(Guid ContractId) : IQuery<GetShiftScheduleByContractIdResult>;

/// <summary>
/// DTO cho Shift Schedule info
/// </summary>
public record ShiftScheduleDto
{
    public Guid Id { get; init; }
    public Guid ContractId { get; init; }
    public Guid? LocationId { get; init; }
    public string ScheduleName { get; init; } = string.Empty;
    public string ScheduleType { get; init; } = string.Empty;

    // Time
    public TimeSpan ShiftStartTime { get; init; }
    public TimeSpan ShiftEndTime { get; init; }
    public bool CrossesMidnight { get; init; }
    public decimal DurationHours { get; init; }
    public int BreakMinutes { get; init; }

    // Staff
    public int GuardsPerShift { get; init; }

    // Recurrence
    public string RecurrenceType { get; init; } = string.Empty;
    public bool AppliesMonday { get; init; }
    public bool AppliesTuesday { get; init; }
    public bool AppliesWednesday { get; init; }
    public bool AppliesThursday { get; init; }
    public bool AppliesFriday { get; init; }
    public bool AppliesSaturday { get; init; }
    public bool AppliesSunday { get; init; }
    public string? MonthlyDates { get; init; }

    // Special Days
    public bool AppliesOnPublicHolidays { get; init; }
    public bool AppliesOnCustomerHolidays { get; init; }
    public bool AppliesOnWeekends { get; init; }
    public bool SkipWhenLocationClosed { get; init; }

    // Requirements
    public bool RequiresArmedGuard { get; init; }
    public bool RequiresSupervisor { get; init; }
    public int MinimumExperienceMonths { get; init; }
    public string? RequiredCertifications { get; init; }

    // Auto Generate
    public bool AutoGenerateEnabled { get; init; }
    public int GenerateAdvanceDays { get; init; }

    // Effective Period
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; }
    public string? Notes { get; init; }
    public string? CreatedBy { get; init; }
}

/// <summary>
/// Kết quả query
/// </summary>
public record GetShiftScheduleByContractIdResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? ContractId { get; init; }
    public string? ContractCode { get; init; }
    public List<ShiftScheduleDto> ShiftSchedules { get; init; } = new();
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// Handler để lấy danh sách shift schedules theo contract ID
/// </summary>
internal class GetShiftScheduleByContractIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetShiftScheduleByContractIdHandler> logger)
    : IQueryHandler<GetShiftScheduleByContractIdQuery, GetShiftScheduleByContractIdResult>
{
    public async Task<GetShiftScheduleByContractIdResult> Handle(
        GetShiftScheduleByContractIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting shift schedules for contract: {ContractId}", request.ContractId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // 1. CHECK IF CONTRACT EXISTS
            // ================================================================
            var contractQuery = @"
                SELECT ContractNumber
                FROM contracts
                WHERE Id = @ContractId AND IsDeleted = 0
            ";

            var contractNumber = await connection.QuerySingleOrDefaultAsync<string>(
                contractQuery,
                new { ContractId = request.ContractId });

            if (contractNumber == null)
            {
                logger.LogWarning("Contract not found: {ContractId}", request.ContractId);
                return new GetShiftScheduleByContractIdResult
                {
                    Success = false,
                    ErrorMessage = $"Contract with ID {request.ContractId} not found"
                };
            }

            // ================================================================
            // 2. GET SHIFT SCHEDULES
            // ================================================================
            var shiftSchedulesQuery = @"
                SELECT
                    Id, ContractId, LocationId, ScheduleName, ScheduleType,
                    ShiftStartTime, ShiftEndTime, CrossesMidnight, DurationHours,
                    BreakMinutes, GuardsPerShift, RecurrenceType,
                    AppliesMonday, AppliesTuesday, AppliesWednesday, AppliesThursday,
                    AppliesFriday, AppliesSaturday, AppliesSunday, MonthlyDates,
                    AppliesOnPublicHolidays, AppliesOnCustomerHolidays, AppliesOnWeekends, SkipWhenLocationClosed,
                    RequiresArmedGuard, RequiresSupervisor, MinimumExperienceMonths, RequiredCertifications,
                    AutoGenerateEnabled, GenerateAdvanceDays,
                    EffectiveFrom, EffectiveTo, IsActive, Notes, CreatedBy
                FROM contract_shift_schedules
                WHERE ContractId = @ContractId AND IsDeleted = 0
                ORDER BY ScheduleName
            ";

            var shiftSchedules = await connection.QueryAsync<ShiftScheduleDto>(
                shiftSchedulesQuery,
                new { ContractId = request.ContractId });

            var shiftSchedulesList = shiftSchedules.ToList();

            logger.LogInformation(
                "Successfully retrieved {Count} shift schedule(s) for contract {ContractNumber}",
                shiftSchedulesList.Count, contractNumber);

            return new GetShiftScheduleByContractIdResult
            {
                Success = true,
                ContractId = request.ContractId,
                ContractCode = contractNumber,
                ShiftSchedules = shiftSchedulesList
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting shift schedules for contract: {ContractId}", request.ContractId);
            return new GetShiftScheduleByContractIdResult
            {
                Success = false,
                ErrorMessage = $"Error getting shift schedules: {ex.Message}"
            };
        }
    }
}
