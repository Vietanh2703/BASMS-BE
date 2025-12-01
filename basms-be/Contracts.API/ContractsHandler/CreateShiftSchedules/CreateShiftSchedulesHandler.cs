namespace Contracts.API.ContractsHandler.CreateShiftSchedules;

// ================================================================
// COMMAND & RESULT
// ================================================================

/// <summary>
/// Command để tạo mới shift schedule
/// </summary>
public record CreateShiftSchedulesCommand : ICommand<CreateShiftSchedulesResult>
{
    public Guid ContractId { get; init; }
    public Guid? LocationId { get; init; }
    public string ScheduleName { get; init; } = string.Empty;
    public string ScheduleType { get; init; } = "regular";

    // Time
    public TimeSpan ShiftStartTime { get; init; }
    public TimeSpan ShiftEndTime { get; init; }
    public bool CrossesMidnight { get; init; }
    public decimal DurationHours { get; init; }
    public int BreakMinutes { get; init; }

    // Staff
    public int GuardsPerShift { get; init; }

    // Recurrence
    public string RecurrenceType { get; init; } = "weekly";
    public bool AppliesMonday { get; init; }
    public bool AppliesTuesday { get; init; }
    public bool AppliesWednesday { get; init; }
    public bool AppliesThursday { get; init; }
    public bool AppliesFriday { get; init; }
    public bool AppliesSaturday { get; init; }
    public bool AppliesSunday { get; init; }
    public string? MonthlyDates { get; init; }

    // Special Days
    public bool AppliesOnPublicHolidays { get; init; } = true;
    public bool AppliesOnCustomerHolidays { get; init; } = true;
    public bool AppliesOnWeekends { get; init; } = true;
    public bool SkipWhenLocationClosed { get; init; }

    // Requirements
    public bool RequiresArmedGuard { get; init; }
    public bool RequiresSupervisor { get; init; }
    public int MinimumExperienceMonths { get; init; }
    public string? RequiredCertifications { get; init; }

    // Auto Generate
    public bool AutoGenerateEnabled { get; init; } = true;
    public int GenerateAdvanceDays { get; init; } = 30;

    // Effective Period
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Notes { get; init; }
}

/// <summary>
/// Kết quả tạo shift schedule
/// </summary>
public record CreateShiftSchedulesResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? ShiftScheduleId { get; init; }
    public string? ScheduleName { get; init; }
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// Handler để tạo mới shift schedule
/// </summary>
internal class CreateShiftSchedulesHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<CreateShiftSchedulesHandler> logger)
    : ICommandHandler<CreateShiftSchedulesCommand, CreateShiftSchedulesResult>
{
    public async Task<CreateShiftSchedulesResult> Handle(
        CreateShiftSchedulesCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Creating shift schedule: {ScheduleName} for Contract: {ContractId}",
                request.ScheduleName, request.ContractId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // 1. CHECK IF CONTRACT EXISTS
            // ================================================================
            var contractExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) > 0 FROM contracts WHERE Id = @ContractId AND IsDeleted = 0",
                new { ContractId = request.ContractId });

            if (!contractExists)
            {
                logger.LogWarning("Contract not found: {ContractId}", request.ContractId);
                return new CreateShiftSchedulesResult
                {
                    Success = false,
                    ErrorMessage = $"Contract with ID {request.ContractId} not found"
                };
            }

            // ================================================================
            // 2. CHECK IF LOCATION EXISTS (if provided)
            // ================================================================
            if (request.LocationId.HasValue)
            {
                var locationExists = await connection.ExecuteScalarAsync<bool>(
                    "SELECT COUNT(*) > 0 FROM customer_locations WHERE Id = @LocationId AND IsDeleted = 0",
                    new { LocationId = request.LocationId.Value });

                if (!locationExists)
                {
                    logger.LogWarning("Location not found: {LocationId}", request.LocationId);
                    return new CreateShiftSchedulesResult
                    {
                        Success = false,
                        ErrorMessage = $"Location with ID {request.LocationId} not found"
                    };
                }
            }

            // ================================================================
            // 3. INSERT SHIFT SCHEDULE
            // ================================================================
            var shiftScheduleId = Guid.NewGuid();
            var insertQuery = @"
                INSERT INTO contract_shift_schedules (
                    Id, ContractId, LocationId, ScheduleName, ScheduleType,
                    ShiftStartTime, ShiftEndTime, CrossesMidnight, DurationHours, BreakMinutes,
                    GuardsPerShift, RecurrenceType,
                    AppliesMonday, AppliesTuesday, AppliesWednesday, AppliesThursday,
                    AppliesFriday, AppliesSaturday, AppliesSunday, MonthlyDates,
                    AppliesOnPublicHolidays, AppliesOnCustomerHolidays, AppliesOnWeekends, SkipWhenLocationClosed,
                    RequiresArmedGuard, RequiresSupervisor, MinimumExperienceMonths, RequiredCertifications,
                    AutoGenerateEnabled, GenerateAdvanceDays,
                    EffectiveFrom, EffectiveTo, IsActive, Notes,
                    IsDeleted, CreatedAt
                ) VALUES (
                    @Id, @ContractId, @LocationId, @ScheduleName, @ScheduleType,
                    @ShiftStartTime, @ShiftEndTime, @CrossesMidnight, @DurationHours, @BreakMinutes,
                    @GuardsPerShift, @RecurrenceType,
                    @AppliesMonday, @AppliesTuesday, @AppliesWednesday, @AppliesThursday,
                    @AppliesFriday, @AppliesSaturday, @AppliesSunday, @MonthlyDates,
                    @AppliesOnPublicHolidays, @AppliesOnCustomerHolidays, @AppliesOnWeekends, @SkipWhenLocationClosed,
                    @RequiresArmedGuard, @RequiresSupervisor, @MinimumExperienceMonths, @RequiredCertifications,
                    @AutoGenerateEnabled, @GenerateAdvanceDays,
                    @EffectiveFrom, @EffectiveTo, @IsActive, @Notes,
                    0, @CreatedAt
                )
            ";

            var rowsAffected = await connection.ExecuteAsync(insertQuery, new
            {
                Id = shiftScheduleId,
                ContractId = request.ContractId,
                LocationId = request.LocationId,
                ScheduleName = request.ScheduleName,
                ScheduleType = request.ScheduleType,
                ShiftStartTime = request.ShiftStartTime,
                ShiftEndTime = request.ShiftEndTime,
                CrossesMidnight = request.CrossesMidnight,
                DurationHours = request.DurationHours,
                BreakMinutes = request.BreakMinutes,
                GuardsPerShift = request.GuardsPerShift,
                RecurrenceType = request.RecurrenceType,
                AppliesMonday = request.AppliesMonday,
                AppliesTuesday = request.AppliesTuesday,
                AppliesWednesday = request.AppliesWednesday,
                AppliesThursday = request.AppliesThursday,
                AppliesFriday = request.AppliesFriday,
                AppliesSaturday = request.AppliesSaturday,
                AppliesSunday = request.AppliesSunday,
                MonthlyDates = request.MonthlyDates,
                AppliesOnPublicHolidays = request.AppliesOnPublicHolidays,
                AppliesOnCustomerHolidays = request.AppliesOnCustomerHolidays,
                AppliesOnWeekends = request.AppliesOnWeekends,
                SkipWhenLocationClosed = request.SkipWhenLocationClosed,
                RequiresArmedGuard = request.RequiresArmedGuard,
                RequiresSupervisor = request.RequiresSupervisor,
                MinimumExperienceMonths = request.MinimumExperienceMonths,
                RequiredCertifications = request.RequiredCertifications,
                AutoGenerateEnabled = request.AutoGenerateEnabled,
                GenerateAdvanceDays = request.GenerateAdvanceDays,
                EffectiveFrom = request.EffectiveFrom,
                EffectiveTo = request.EffectiveTo,
                IsActive = request.IsActive,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            });

            if (rowsAffected == 0)
            {
                logger.LogWarning("Failed to insert shift schedule");
                return new CreateShiftSchedulesResult
                {
                    Success = false,
                    ErrorMessage = "Failed to create shift schedule"
                };
            }

            logger.LogInformation("Successfully created shift schedule: {ShiftScheduleId} - {ScheduleName}",
                shiftScheduleId, request.ScheduleName);

            return new CreateShiftSchedulesResult
            {
                Success = true,
                ShiftScheduleId = shiftScheduleId,
                ScheduleName = request.ScheduleName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating shift schedule: {ScheduleName}", request.ScheduleName);
            return new CreateShiftSchedulesResult
            {
                Success = false,
                ErrorMessage = $"Error creating shift schedule: {ex.Message}"
            };
        }
    }
}
