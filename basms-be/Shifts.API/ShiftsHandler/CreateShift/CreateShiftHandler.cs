using Shifts.API.Services;

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
    ContractsApiClient contractsApiClient,
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
            // BƯỚC 1: VALIDATE CONTRACT (nếu có)
            // ================================================================
            if (request.ContractId.HasValue)
            {
                logger.LogInformation("Validating contract {ContractId}", request.ContractId);

                var contractValidation = await contractsApiClient.ValidateContractAsync(
                    request.ContractId.Value);

                if (contractValidation == null || !contractValidation.IsValid)
                {
                    var error = contractValidation?.ErrorMessage ?? "Failed to validate contract";
                    logger.LogWarning("Contract validation failed: {Error}", error);
                    throw new InvalidOperationException($"Contract validation failed: {error}");
                }

                logger.LogInformation(
                    "✓ Contract {ContractNumber} is valid",
                    contractValidation.Contract?.ContractNumber);

                // ============================================================
                // BƯỚC 2: VALIDATE LOCATION thuộc contract
                // ============================================================
                logger.LogInformation(
                    "Validating location {LocationId} for contract {ContractId}",
                    request.LocationId,
                    request.ContractId);

                var locationValidation = await contractsApiClient.ValidateLocationAsync(
                    request.ContractId.Value,
                    request.LocationId);

                if (locationValidation == null || !locationValidation.IsValid)
                {
                    var error = locationValidation?.ErrorMessage ?? "Failed to validate location";
                    logger.LogWarning("Location validation failed: {Error}", error);
                    throw new InvalidOperationException($"Location validation failed: {error}");
                }

                logger.LogInformation(
                    "✓ Location {LocationCode} is valid for contract",
                    locationValidation.Location?.LocationCode);
            }

            // ================================================================
            // BƯỚC 3: CHECK PUBLIC HOLIDAY
            // ================================================================
            logger.LogInformation("Checking if {ShiftDate:yyyy-MM-dd} is public holiday", request.ShiftDate);

            var holidayCheck = await contractsApiClient.CheckPublicHolidayAsync(request.ShiftDate);
            bool isPublicHoliday = holidayCheck?.IsHoliday ?? false;
            bool isTetHoliday = holidayCheck?.IsTetPeriod ?? false;

            if (isPublicHoliday)
            {
                logger.LogInformation(
                    "Shift date {ShiftDate:yyyy-MM-dd} is {HolidayName}",
                    request.ShiftDate,
                    holidayCheck?.HolidayName);
            }

            // ================================================================
            // BƯỚC 4: CHECK LOCATION CLOSED
            // ================================================================
            logger.LogInformation(
                "Checking if location {LocationId} is closed on {ShiftDate:yyyy-MM-dd}",
                request.LocationId,
                request.ShiftDate);

            var locationClosedCheck = await contractsApiClient.CheckLocationClosedAsync(
                request.LocationId,
                request.ShiftDate);

            if (locationClosedCheck?.IsClosed == true)
            {
                logger.LogWarning(
                    "Location is closed on {ShiftDate:yyyy-MM-dd}: {Reason}",
                    request.ShiftDate,
                    locationClosedCheck.Reason);

                // Có thể warning hoặc throw exception tùy business rule
                // throw new InvalidOperationException($"Location is closed: {locationClosedCheck.Reason}");
            }

            // ================================================================
            // BƯỚC 5: TẠO SHIFT
            // ================================================================
            using var connection = await dbFactory.CreateConnectionAsync();

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
