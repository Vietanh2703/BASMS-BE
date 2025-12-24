namespace Contracts.API.ContractsHandler.UpdateShiftSchedules;

public class UpdateShiftSchedulesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/contracts/shift-schedules/{shiftScheduleId}", async (
            Guid shiftScheduleId,
            UpdateShiftSchedulesRequest request,
            ISender sender,
            ILogger<UpdateShiftSchedulesEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Update shift schedule request for ID: {ShiftScheduleId}", shiftScheduleId);

                var command = new UpdateShiftSchedulesCommand
                {
                    ShiftScheduleId = shiftScheduleId,
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
                    Notes = request.Notes
                };

                var result = await sender.Send(command);

                if (!result.Success)
                {
                    logger.LogError("Failed to update shift schedule: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error updating shift schedule",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }

                logger.LogInformation("Successfully updated shift schedule: {ShiftScheduleId}", result.ShiftScheduleId);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing update shift schedule request for ID: {ShiftScheduleId}", shiftScheduleId);
                return Results.Problem(
                    title: "Error updating shift schedule",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Shift Schedules")
        .WithName("UpdateShiftSchedules")
        .Produces<UpdateShiftSchedulesResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Update thông tin shift schedule");
    }
}


public record UpdateShiftSchedulesRequest
{
    public string ScheduleName { get; init; } = string.Empty;
    public string ScheduleType { get; init; } = "regular";
    public TimeSpan ShiftStartTime { get; init; }
    public TimeSpan ShiftEndTime { get; init; }
    public bool CrossesMidnight { get; init; }
    public decimal DurationHours { get; init; }
    public int BreakMinutes { get; init; }
    public int GuardsPerShift { get; init; }
    public string RecurrenceType { get; init; } = "weekly";
    public bool AppliesMonday { get; init; }
    public bool AppliesTuesday { get; init; }
    public bool AppliesWednesday { get; init; }
    public bool AppliesThursday { get; init; }
    public bool AppliesFriday { get; init; }
    public bool AppliesSaturday { get; init; }
    public bool AppliesSunday { get; init; }
    public string? MonthlyDates { get; init; }
    public bool AppliesOnPublicHolidays { get; init; } = true;
    public bool AppliesOnCustomerHolidays { get; init; } = true;
    public bool AppliesOnWeekends { get; init; } = true;
    public bool SkipWhenLocationClosed { get; init; }
    public bool RequiresArmedGuard { get; init; }
    public bool RequiresSupervisor { get; init; }
    public int MinimumExperienceMonths { get; init; }
    public string? RequiredCertifications { get; init; }
    public bool AutoGenerateEnabled { get; init; } = true;
    public int GenerateAdvanceDays { get; init; } = 30;
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Notes { get; init; }
}
