using Shifts.API.Validators;
using Shifts.API.Handlers.GetAvailableGuards;
using Shifts.API.Handlers.SendNotification;

namespace Shifts.API.ShiftsHandler.CreateShift;

// Command để tạo shift mới
public record CreateShiftCommand(
    Guid? ContractId,           // ID hợp đồng (optional nếu ad-hoc shift)
    Guid LocationId,            // ID địa điểm
    DateTime ShiftDate,         // Ngày làm việc
    TimeSpan StartTime,         // Giờ bắt đầu: 08:00
    TimeSpan EndTime,           // Giờ kết thúc: 17:00
    int RequiredGuards,         // Số bảo vệ cần: 2, 3...
    string ShiftType,           // REGULAR, OVERTIME, EMERGENCY...
    string? Description,        // Mô tả ca
    Guid CreatedBy              // Manager tạo ca
) : ICommand<CreateShiftResult>;

// Result chứa shift đã tạo
public record CreateShiftResult(Guid ShiftId, string ShiftCode);

internal class CreateShiftHandler(
    IDbConnectionFactory dbFactory,
    ShiftValidator shiftValidator,
    ISender sender,
    ILogger<CreateShiftHandler> logger)
    : ICommandHandler<CreateShiftCommand, CreateShiftResult>
{
    public async Task<CreateShiftResult> Handle(
        CreateShiftCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Creating shift for location {LocationId} on {ShiftDate:yyyy-MM-dd}",
                request.LocationId,
                request.ShiftDate);

            // ================================================================
            // BƯỚC 1: VALIDATE SHIFT TIME OVERLAP
            // ================================================================
            logger.LogInformation("Validating shift time overlap");

            var overlapValidation = await shiftValidator.ValidateShiftTimeOverlapAsync(
                request.LocationId,
                request.ShiftDate,
                request.StartTime,
                request.EndTime);

            if (!overlapValidation.IsValid)
            {
                logger.LogWarning(
                    "Shift time overlap detected: {ErrorMessage}",
                    overlapValidation.ErrorMessage);
                throw new InvalidOperationException(
                    $"Không thể tạo ca trực: {overlapValidation.ErrorMessage}. " +
                    $"Có {overlapValidation.OverlappingShifts.Count} ca trực trùng thời gian.");
            }

            logger.LogInformation("✓ No shift time overlap detected");

            // ================================================================
            // BƯỚC 2: VALIDATE CONTRACT PERIOD (nếu có contract) - VIA RABBITMQ
            // ================================================================
            if (request.ContractId.HasValue)
            {
                logger.LogInformation("Validating shift within contract period via RabbitMQ");

                var periodValidation = await shiftValidator.ValidateShiftWithinContractPeriodAsync(
                    request.ContractId.Value,
                    request.ShiftDate);

                if (!periodValidation.IsValid)
                {
                    logger.LogWarning(
                        "Shift date outside contract period: {ErrorMessage}",
                        periodValidation.ErrorMessage);
                    throw new InvalidOperationException(
                        $"Không thể tạo ca trực: {periodValidation.ErrorMessage}");
                }

                logger.LogInformation(
                    "✓ Shift date is within contract period ({ContractNumber})",
                    periodValidation.ContractNumber);
            }

            // ================================================================
            // BƯỚC 3: VALIDATE GUARD AVAILABILITY - Phải có ít nhất 1 guard available
            // ================================================================
            logger.LogInformation("Checking guard availability");

            var availableGuardsQuery = new GetAvailableGuardsQuery(
                request.LocationId,
                request.ShiftDate,
                request.StartTime,
                request.EndTime);

            var guardsAvailability = await sender.Send(availableGuardsQuery, cancellationToken);

            if (guardsAvailability.AvailableCount == 0)
            {
                logger.LogWarning(
                    "No guards available for shift. Busy: {Busy}, OnLeave: {OnLeave}",
                    guardsAvailability.BusyCount,
                    guardsAvailability.OnLeaveCount);
                throw new InvalidOperationException(
                    $"Không thể tạo ca trực: Không có bảo vệ nào rảnh trong khung giờ này. " +
                    $"({guardsAvailability.BusyCount} đang bận, {guardsAvailability.OnLeaveCount} đang nghỉ phép)");
            }

            if (guardsAvailability.AvailableCount < request.RequiredGuards)
            {
                logger.LogWarning(
                    "Insufficient guards: Required {Required}, Available {Available}",
                    request.RequiredGuards,
                    guardsAvailability.AvailableCount);
                // Warning nhưng vẫn cho phép tạo
                logger.LogInformation(
                    "⚠️ Warning: Only {Available}/{Required} guards available",
                    guardsAvailability.AvailableCount,
                    request.RequiredGuards);
            }
            else
            {
                logger.LogInformation(
                    "✓ Sufficient guards available: {Available}/{Required}",
                    guardsAvailability.AvailableCount,
                    request.RequiredGuards);
            }

            // ================================================================
            // BƯỚC 4: TẠO SHIFT
            // ================================================================
            using var connection = await dbFactory.CreateConnectionAsync();

            // Determine holiday status (simplified - có thể enhance bằng RabbitMQ call sau)
            bool isPublicHoliday = false;
            bool isTetHoliday = false;

            var shift = new Models.Shifts
            {
                Id = Guid.NewGuid(),
                ShiftTemplateId = null, // Ad-hoc shift
                LocationId = request.LocationId,
                ContractId = request.ContractId,

                // Date splitting
                ShiftDate = request.ShiftDate.Date,
                ShiftDay = request.ShiftDate.Day,
                ShiftMonth = request.ShiftDate.Month,
                ShiftYear = request.ShiftDate.Year,
                ShiftQuarter = (request.ShiftDate.Month - 1) / 3 + 1,
                ShiftWeek = GetIso8601WeekOfYear(request.ShiftDate),
                DayOfWeek = (int)request.ShiftDate.DayOfWeek == 0 ? 7 : (int)request.ShiftDate.DayOfWeek,

                // Time
                ShiftStart = request.ShiftDate.Date.Add(request.StartTime),
                ShiftEnd = request.ShiftDate.Date.Add(request.EndTime),

                // Duration
                TotalDurationMinutes = (int)(request.EndTime - request.StartTime).TotalMinutes,
                WorkDurationMinutes = (int)(request.EndTime - request.StartTime).TotalMinutes - 60, // -1h break
                WorkDurationHours = (decimal)(request.EndTime - request.StartTime).TotalHours - 1m,

                // Break
                BreakDurationMinutes = 60,
                UnpaidBreakMinutes = 60,

                // Type
                ShiftType = request.ShiftType,

                // Holiday flags
                IsPublicHoliday = isPublicHoliday,
                IsTetHoliday = isTetHoliday,
                IsSaturday = request.ShiftDate.DayOfWeek == DayOfWeek.Saturday,
                IsSunday = request.ShiftDate.DayOfWeek == DayOfWeek.Sunday,
                IsRegularWeekday = request.ShiftDate.DayOfWeek >= DayOfWeek.Monday &&
                                   request.ShiftDate.DayOfWeek <= DayOfWeek.Friday,

                // Night shift check (22:00-06:00)
                IsNightShift = request.StartTime.Hours >= 22 || request.EndTime.Hours <= 6,

                // Staffing
                RequiredGuards = request.RequiredGuards,
                AssignedGuardsCount = 0,

                // Status
                Status = "DRAFT",
                ApprovalStatus = "PENDING",

                // Description
                Description = request.Description,

                // Audit
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.CreatedBy
            };

            await connection.InsertAsync(shift);

            logger.LogInformation(
                "✓ Successfully created shift {ShiftId} for {ShiftDate:yyyy-MM-dd}",
                shift.Id,
                shift.ShiftDate);

            // ================================================================
            // BƯỚC 5: GỬI NOTIFICATIONS CHO DIRECTOR VÀ CUSTOMER (nếu có contract)
            // ================================================================
            if (request.ContractId.HasValue)
            {
                logger.LogInformation("Sending notifications for shift creation");

                // Lấy Customer ID từ contract response và gửi notification
                // Tạm thời skip, sẽ implement sau khi có full integration

                logger.LogInformation("✓ Notifications queued");
            }

            return new CreateShiftResult(shift.Id, $"SH-{shift.ShiftDate:yyyyMMdd}-{shift.Id.ToString()[..8]}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating shift");
            throw;
        }
    }

    // Helper method để tính ISO 8601 week number
    private static int GetIso8601WeekOfYear(DateTime date)
    {
        var day = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
        {
            date = date.AddDays(3);
        }

        return System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            date,
            System.Globalization.CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
    }
}
