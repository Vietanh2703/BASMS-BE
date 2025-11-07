using Shifts.API.Services;

namespace Shifts.API.ShiftsHandler.UpdateShift;

// Command để update shift
public record UpdateShiftCommand(
    Guid ShiftId,
    DateTime? ShiftDate,        // Optional - chỉ update nếu khác null
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    int? RequiredGuards,
    string? Description,
    Guid UpdatedBy
) : ICommand<UpdateShiftResult>;

// Result
public record UpdateShiftResult(bool Success, string Message);

internal class UpdateShiftHandler(
    IDbConnectionFactory dbFactory,
    ContractsApiClient contractsApiClient,
    ILogger<UpdateShiftHandler> logger)
    : ICommandHandler<UpdateShiftCommand, UpdateShiftResult>
{
    public async Task<UpdateShiftResult> Handle(
        UpdateShiftCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Updating shift {ShiftId}", request.ShiftId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: LẤY SHIFT HIỆN TẠI
            // ================================================================
            var shift = await connection.GetAsync<Models.Shifts>(request.ShiftId);

            if (shift == null || shift.IsDeleted)
            {
                logger.LogWarning("Shift {ShiftId} not found", request.ShiftId);
                throw new InvalidOperationException($"Shift {request.ShiftId} not found");
            }

            logger.LogInformation(
                "Found shift {ShiftId} at location {LocationId}",
                shift.Id,
                shift.LocationId);

            // ================================================================
            // BƯỚC 2: NẾU THAY ĐỔI NGÀY -> VALIDATE LẠI
            // ================================================================
            if (request.ShiftDate.HasValue && request.ShiftDate.Value.Date != shift.ShiftDate.Date)
            {
                logger.LogInformation(
                    "Shift date changing from {OldDate:yyyy-MM-dd} to {NewDate:yyyy-MM-dd}",
                    shift.ShiftDate,
                    request.ShiftDate.Value);

                // Check public holiday cho ngày mới
                var holidayCheck = await contractsApiClient.CheckPublicHolidayAsync(
                    request.ShiftDate.Value);

                if (holidayCheck?.IsHoliday == true)
                {
                    logger.LogInformation(
                        "New shift date {ShiftDate:yyyy-MM-dd} is {HolidayName}",
                        request.ShiftDate.Value,
                        holidayCheck.HolidayName);

                    shift.IsPublicHoliday = true;
                    shift.IsTetHoliday = holidayCheck.IsTetPeriod;
                }
                else
                {
                    shift.IsPublicHoliday = false;
                    shift.IsTetHoliday = false;
                }

                // Check location closed
                var locationClosedCheck = await contractsApiClient.CheckLocationClosedAsync(
                    shift.LocationId,
                    request.ShiftDate.Value);

                if (locationClosedCheck?.IsClosed == true)
                {
                    logger.LogWarning(
                        "Location is closed on new date {ShiftDate:yyyy-MM-dd}: {Reason}",
                        request.ShiftDate.Value,
                        locationClosedCheck.Reason);

                    // Business rule: có thể cho phép hoặc block
                    // throw new InvalidOperationException($"Location is closed: {locationClosedCheck.Reason}");
                }

                // Update date fields
                shift.ShiftDate = request.ShiftDate.Value.Date;
                shift.ShiftDay = request.ShiftDate.Value.Day;
                shift.ShiftMonth = request.ShiftDate.Value.Month;
                shift.ShiftYear = request.ShiftDate.Value.Year;
                shift.ShiftQuarter = (request.ShiftDate.Value.Month - 1) / 3 + 1;
                shift.DayOfWeek = (int)request.ShiftDate.Value.DayOfWeek == 0 ? 7 : (int)request.ShiftDate.Value.DayOfWeek;
                shift.IsSaturday = request.ShiftDate.Value.DayOfWeek == DayOfWeek.Saturday;
                shift.IsSunday = request.ShiftDate.Value.DayOfWeek == DayOfWeek.Sunday;
                shift.IsRegularWeekday = request.ShiftDate.Value.DayOfWeek >= DayOfWeek.Monday &&
                                        request.ShiftDate.Value.DayOfWeek <= DayOfWeek.Friday;
            }

            // ================================================================
            // BƯỚC 3: UPDATE CÁC FIELDS KHÁC
            // ================================================================
            bool hasChanges = false;

            if (request.StartTime.HasValue)
            {
                shift.ShiftStart = shift.ShiftDate.Add(request.StartTime.Value);
                hasChanges = true;
            }

            if (request.EndTime.HasValue)
            {
                shift.ShiftEnd = shift.ShiftDate.Add(request.EndTime.Value);
                hasChanges = true;
            }

            // Recalculate duration if time changed
            if (request.StartTime.HasValue || request.EndTime.HasValue)
            {
                var duration = shift.ShiftEnd - shift.ShiftStart;
                shift.TotalDurationMinutes = (int)duration.TotalMinutes;
                shift.WorkDurationMinutes = shift.TotalDurationMinutes - shift.BreakDurationMinutes;
                shift.WorkDurationHours = (decimal)shift.WorkDurationMinutes / 60m;

                // Check night shift
                shift.IsNightShift = shift.ShiftStart.Hour >= 22 || shift.ShiftEnd.Hour <= 6;
            }

            if (request.RequiredGuards.HasValue)
            {
                shift.RequiredGuards = request.RequiredGuards.Value;
                hasChanges = true;
            }

            if (request.Description != null)
            {
                shift.Description = request.Description;
                hasChanges = true;
            }

            if (!hasChanges && !request.ShiftDate.HasValue)
            {
                logger.LogInformation("No changes to update for shift {ShiftId}", shift.Id);
                return new UpdateShiftResult(true, "No changes detected");
            }

            // ================================================================
            // BƯỚC 4: SAVE CHANGES
            // ================================================================
            shift.UpdatedAt = DateTime.UtcNow;
            shift.UpdatedBy = request.UpdatedBy;
            shift.Version++; // Optimistic locking

            await connection.UpdateAsync(shift);

            logger.LogInformation("✓ Successfully updated shift {ShiftId}", shift.Id);

            return new UpdateShiftResult(true, "Shift updated successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating shift {ShiftId}", request.ShiftId);
            throw;
        }
    }
}
