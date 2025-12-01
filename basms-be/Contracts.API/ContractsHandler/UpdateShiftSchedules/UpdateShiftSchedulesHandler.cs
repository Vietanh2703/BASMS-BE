namespace Contracts.API.ContractsHandler.UpdateShiftSchedules;

// ================================================================
// COMMAND & RESULT
// ================================================================

/// <summary>
/// Command để update shift schedule
/// </summary>
public record UpdateShiftSchedulesCommand : ICommand<UpdateShiftSchedulesResult>
{
    public Guid ShiftScheduleId { get; init; }
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
/// Kết quả update shift schedule
/// </summary>
public record UpdateShiftSchedulesResult
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
/// Handler để update shift schedule
/// </summary>
internal class UpdateShiftSchedulesHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<UpdateShiftSchedulesHandler> logger)
    : ICommandHandler<UpdateShiftSchedulesCommand, UpdateShiftSchedulesResult>
{
    public async Task<UpdateShiftSchedulesResult> Handle(
        UpdateShiftSchedulesCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Updating shift schedule: {ShiftScheduleId}", request.ShiftScheduleId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // 1. CHECK IF SHIFT SCHEDULE EXISTS
            // ================================================================
            var existingSchedule = await connection.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT Id, ScheduleName FROM contract_shift_schedules WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.ShiftScheduleId });

            if (existingSchedule == null)
            {
                logger.LogWarning("Shift schedule not found: {ShiftScheduleId}", request.ShiftScheduleId);
                return new UpdateShiftSchedulesResult
                {
                    Success = false,
                    ErrorMessage = $"Shift schedule with ID {request.ShiftScheduleId} not found"
                };
            }

            // ================================================================
            // 2. UPDATE SHIFT SCHEDULE
            // ================================================================
            var updateQuery = @"
                UPDATE contract_shift_schedules SET
                    ScheduleName = @ScheduleName,
                    ScheduleType = @ScheduleType,
                    ShiftStartTime = @ShiftStartTime,
                    ShiftEndTime = @ShiftEndTime,
                    CrossesMidnight = @CrossesMidnight,
                    DurationHours = @DurationHours,
                    BreakMinutes = @BreakMinutes,
                    GuardsPerShift = @GuardsPerShift,
                    RecurrenceType = @RecurrenceType,
                    AppliesMonday = @AppliesMonday,
                    AppliesTuesday = @AppliesTuesday,
                    AppliesWednesday = @AppliesWednesday,
                    AppliesThursday = @AppliesThursday,
                    AppliesFriday = @AppliesFriday,
                    AppliesSaturday = @AppliesSaturday,
                    AppliesSunday = @AppliesSunday,
                    MonthlyDates = @MonthlyDates,
                    AppliesOnPublicHolidays = @AppliesOnPublicHolidays,
                    AppliesOnCustomerHolidays = @AppliesOnCustomerHolidays,
                    AppliesOnWeekends = @AppliesOnWeekends,
                    SkipWhenLocationClosed = @SkipWhenLocationClosed,
                    RequiresArmedGuard = @RequiresArmedGuard,
                    RequiresSupervisor = @RequiresSupervisor,
                    MinimumExperienceMonths = @MinimumExperienceMonths,
                    RequiredCertifications = @RequiredCertifications,
                    AutoGenerateEnabled = @AutoGenerateEnabled,
                    GenerateAdvanceDays = @GenerateAdvanceDays,
                    EffectiveFrom = @EffectiveFrom,
                    EffectiveTo = @EffectiveTo,
                    IsActive = @IsActive,
                    Notes = @Notes,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @ShiftScheduleId AND IsDeleted = 0
            ";

            var rowsAffected = await connection.ExecuteAsync(updateQuery, new
            {
                ShiftScheduleId = request.ShiftScheduleId,
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
                UpdatedAt = DateTime.UtcNow
            });

            if (rowsAffected == 0)
            {
                logger.LogWarning("No rows affected when updating shift schedule: {ShiftScheduleId}", request.ShiftScheduleId);
                return new UpdateShiftSchedulesResult
                {
                    Success = false,
                    ErrorMessage = "Failed to update shift schedule"
                };
            }

            logger.LogInformation("Successfully updated shift schedule: {ShiftScheduleId} - {ScheduleName}",
                request.ShiftScheduleId, request.ScheduleName);

            return new UpdateShiftSchedulesResult
            {
                Success = true,
                ShiftScheduleId = request.ShiftScheduleId,
                ScheduleName = request.ScheduleName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating shift schedule: {ShiftScheduleId}", request.ShiftScheduleId);
            return new UpdateShiftSchedulesResult
            {
                Success = false,
                ErrorMessage = $"Error updating shift schedule: {ex.Message}"
            };
        }
    }
}
