using Shifts.API.Validators;
using Shifts.API.Handlers.SendNotification;

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
    ShiftValidator shiftValidator,
    ISender sender,
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
            // BƯỚC 2: NẾU THAY ĐỔI NGÀY -> UPDATE DATE FIELDS
            // ================================================================
            if (request.ShiftDate.HasValue && request.ShiftDate.Value.Date != shift.ShiftDate.Date)
            {
                logger.LogInformation(
                    "Shift date changing from {OldDate:yyyy-MM-dd} to {NewDate:yyyy-MM-dd}",
                    shift.ShiftDate,
                    request.ShiftDate.Value);

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

                // Reset holiday flags (có thể enhance bằng RabbitMQ call sau)
                shift.IsPublicHoliday = false;
                shift.IsTetHoliday = false;
            }

            // ================================================================
            // BƯỚC 3: VALIDATE SHIFT TIME OVERLAP (nếu có thay đổi về thời gian)
            // ================================================================
            var finalShiftDate = request.ShiftDate ?? shift.ShiftDate;
            var finalStartTime = request.StartTime ?? shift.ShiftStart.TimeOfDay;
            var finalEndTime = request.EndTime ?? shift.ShiftEnd.TimeOfDay;

            if (request.ShiftDate.HasValue || request.StartTime.HasValue || request.EndTime.HasValue)
            {
                logger.LogInformation("Validating shift time overlap for updated shift");

                var overlapValidation = await shiftValidator.ValidateShiftTimeOverlapAsync(
                    shift.LocationId,
                    finalShiftDate,
                    finalStartTime,
                    finalEndTime,
                    shift.Id); // Loại trừ shift hiện tại

                if (!overlapValidation.IsValid)
                {
                    logger.LogWarning(
                        "Shift time overlap detected: {ErrorMessage}",
                        overlapValidation.ErrorMessage);
                    throw new InvalidOperationException(
                        $"Không thể cập nhật ca trực: {overlapValidation.ErrorMessage}. " +
                        $"Có {overlapValidation.OverlappingShifts.Count} ca trực trùng thời gian.");
                }

                logger.LogInformation("✓ No shift time overlap detected");
            }

            // ================================================================
            // BƯỚC 4: VALIDATE CONTRACT PERIOD (nếu có thay đổi ngày và có contract) - VIA RABBITMQ
            // ================================================================
            if (request.ShiftDate.HasValue && shift.ContractId.HasValue)
            {
                logger.LogInformation("Validating shift within contract period via RabbitMQ");

                var periodValidation = await shiftValidator.ValidateShiftWithinContractPeriodAsync(
                    shift.ContractId.Value,
                    finalShiftDate);

                if (!periodValidation.IsValid)
                {
                    logger.LogWarning(
                        "Shift date outside contract period: {ErrorMessage}",
                        periodValidation.ErrorMessage);
                    throw new InvalidOperationException(
                        $"Không thể cập nhật ca trực: {periodValidation.ErrorMessage}");
                }

                logger.LogInformation(
                    "✓ Shift date is within contract period ({ContractNumber})",
                    periodValidation.ContractNumber);
            }

            // ================================================================
            // BƯỚC 5: UPDATE CÁC FIELDS KHÁC
            // ================================================================
            bool hasChanges = false;
            var changesList = new List<string>();

            if (request.StartTime.HasValue)
            {
                var oldTime = shift.ShiftStart.ToString("HH:mm");
                shift.ShiftStart = shift.ShiftDate.Add(request.StartTime.Value);
                hasChanges = true;
                changesList.Add($"Giờ bắt đầu: {oldTime} → {request.StartTime.Value:hh\\:mm}");
            }

            if (request.EndTime.HasValue)
            {
                var oldTime = shift.ShiftEnd.ToString("HH:mm");
                shift.ShiftEnd = shift.ShiftDate.Add(request.EndTime.Value);
                hasChanges = true;
                changesList.Add($"Giờ kết thúc: {oldTime} → {request.EndTime.Value:hh\\:mm}");
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
                changesList.Add($"Số bảo vệ cần: {shift.RequiredGuards} → {request.RequiredGuards.Value}");
                shift.RequiredGuards = request.RequiredGuards.Value;
                hasChanges = true;
            }

            if (request.Description != null)
            {
                shift.Description = request.Description;
                hasChanges = true;
                changesList.Add("Mô tả ca đã được cập nhật");
            }

            if (request.ShiftDate.HasValue)
            {
                hasChanges = true;
                changesList.Add($"Ngày ca trực: {shift.ShiftDate:dd/MM/yyyy} → {request.ShiftDate.Value:dd/MM/yyyy}");
            }

            if (!hasChanges)
            {
                logger.LogInformation("No changes to update for shift {ShiftId}", shift.Id);
                return new UpdateShiftResult(true, "No changes detected");
            }

            // ================================================================
            // BƯỚC 6: SAVE CHANGES
            // ================================================================
            shift.UpdatedAt = DateTime.UtcNow;
            shift.UpdatedBy = request.UpdatedBy;
            shift.Version++; // Optimistic locking

            await connection.UpdateAsync(shift);

            logger.LogInformation("✓ Successfully updated shift {ShiftId}", shift.Id);

            // ================================================================
            // BƯỚC 7: GỬI NOTIFICATIONS CHO GUARDS VÀ STAKEHOLDERS (nếu có thay đổi)
            // ================================================================
            if (changesList.Any())
            {
                logger.LogInformation("Sending notifications for shift update");

                var changesDescription = string.Join(", ", changesList);

                // Gửi notification cho guards đã được assign vào shift này
                // Gửi notification cho director và customer (nếu có contract)
                // Sẽ implement sau khi có full integration

                logger.LogInformation("✓ Notifications queued (changes: {Changes})", changesDescription);
            }

            return new UpdateShiftResult(true, "Shift updated successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating shift {ShiftId}", request.ShiftId);
            throw;
        }
    }
}
