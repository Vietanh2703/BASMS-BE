using Shifts.API.ShiftsHandler.GetAvailableGuards;

namespace Shifts.API.ShiftsHandler.CreateShift;


public record CreateShiftCommand(
    Guid? ContractId,           
    Guid LocationId,            
    DateTime ShiftDate,         
    TimeSpan StartTime,         
    TimeSpan EndTime,           
    int RequiredGuards,         
    string ShiftType,           
    string? Description,        
    Guid CreatedBy              
) : ICommand<CreateShiftResult>;

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

            var today = DateTime.UtcNow.Date;
            if (request.ShiftDate.Date <= today)
            {
                logger.LogWarning(
                    "Shift date {ShiftDate:yyyy-MM-dd} is not after today (today: {Today:yyyy-MM-dd})",
                    request.ShiftDate,
                    today);
                throw new InvalidOperationException(
                    $"Không thể tạo ca trực với ngày {request.ShiftDate:yyyy-MM-dd}. " +
                    $"Ngày ca trực phải sau ngày hôm nay ({today:yyyy-MM-dd}).");
            }

            logger.LogInformation("✓ Shift date validation passed");
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

            logger.LogInformation(" No shift time overlap detected");
            
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
                logger.LogInformation(
                    "Warning: Only {Available}/{Required} guards available",
                    guardsAvailability.AvailableCount,
                    request.RequiredGuards);
            }
            else
            {
                logger.LogInformation(
                    "Sufficient guards available: {Available}/{Required}",
                    guardsAvailability.AvailableCount,
                    request.RequiredGuards);
            }

            using var connection = await dbFactory.CreateConnectionAsync();

            bool isPublicHoliday = false;
            bool isTetHoliday = false;

            var shift = new Models.Shifts
            {
                Id = Guid.NewGuid(),
                ShiftTemplateId = null,
                LocationId = request.LocationId,
                ContractId = request.ContractId,
                ShiftDate = request.ShiftDate.Date,
                ShiftDay = request.ShiftDate.Day,
                ShiftMonth = request.ShiftDate.Month,
                ShiftYear = request.ShiftDate.Year,
                ShiftQuarter = (request.ShiftDate.Month - 1) / 3 + 1,
                ShiftWeek = GetIso8601WeekOfYear(request.ShiftDate),
                DayOfWeek = (int)request.ShiftDate.DayOfWeek == 0 ? 7 : (int)request.ShiftDate.DayOfWeek,
                ShiftStart = request.ShiftDate.Date.Add(request.StartTime),
                ShiftEnd = request.ShiftDate.Date.Add(request.EndTime),
                TotalDurationMinutes = (int)(request.EndTime - request.StartTime).TotalMinutes,
                WorkDurationMinutes = (int)(request.EndTime - request.StartTime).TotalMinutes - 60, // -1h break
                WorkDurationHours = (decimal)(request.EndTime - request.StartTime).TotalHours - 1m,
                BreakDurationMinutes = 60,
                UnpaidBreakMinutes = 60,
                ShiftType = request.ShiftType,
                IsPublicHoliday = isPublicHoliday,
                IsTetHoliday = isTetHoliday,
                IsSaturday = request.ShiftDate.DayOfWeek == DayOfWeek.Saturday,
                IsSunday = request.ShiftDate.DayOfWeek == DayOfWeek.Sunday,
                IsRegularWeekday = request.ShiftDate.DayOfWeek >= DayOfWeek.Monday &&
                                   request.ShiftDate.DayOfWeek <= DayOfWeek.Friday,
                IsNightShift = request.StartTime.Hours >= 22 || request.EndTime.Hours <= 6,
                RequiredGuards = request.RequiredGuards,
                AssignedGuardsCount = 0,
                Status = "DRAFT",
                ApprovalStatus = "PENDING",
                Description = request.Description,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.CreatedBy
            };

            await connection.InsertAsync(shift);

            logger.LogInformation(
                "✓ Successfully created shift {ShiftId} for {ShiftDate:yyyy-MM-dd}",
                shift.Id,
                shift.ShiftDate);
            if (request.ContractId.HasValue)
            {
                logger.LogInformation("Sending notifications for shift creation");

                logger.LogInformation("Notifications queued");
            }

            return new CreateShiftResult(shift.Id, $"SH-{shift.ShiftDate:yyyyMMdd}-{shift.Id.ToString()[..8]}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating shift");
            throw;
        }
    }
    private static int GetIso8601WeekOfYear(DateTime date)
    {
        var day = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
        {
            date = date.AddDays(3);
        }

        return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            date,
            CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
    }
}
