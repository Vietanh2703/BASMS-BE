using BuildingBlocks.Messaging.Events;
using Dapper;

namespace Shifts.API.ShiftsHandler.GenerateShifts;

/// <summary>
/// Handler tự động tạo ca làm từ contract shift schedules
///
/// WORKFLOW:
/// 1. Nhận ContractId và số ngày cần generate
/// 2. Lấy tất cả shift schedules và locations của contract
/// 3. Với mỗi ngày trong khoảng thời gian:
///    - Kiểm tra ngày lễ (call Contracts.API)
///    - Kiểm tra location đóng cửa (call Contracts.API)
///    - Kiểm tra shift exception (skip/modify)
///    - Kiểm tra ngày trong tuần (Monday-Sunday flags)
///    - Tạo shift nếu pass tất cả điều kiện
/// 4. Return kết quả: số ca đã tạo, số ca bỏ qua, lý do
/// </summary>
public class GenerateShiftsHandler : ICommandHandler<GenerateShiftsCommand, GenerateShiftsResult>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<GenerateShiftsHandler> _logger;
    private readonly IRequestClient<CheckPublicHolidayRequest> _holidayRequestClient;
    private readonly IRequestClient<CheckLocationClosedRequest> _locationClosedRequestClient;
    private readonly IRequestClient<GetContractShiftSchedulesRequest> _schedulesRequestClient;
    private readonly IPublishEndpoint _publishEndpoint;

    public GenerateShiftsHandler(
        IDbConnectionFactory dbFactory,
        ILogger<GenerateShiftsHandler> logger,
        IRequestClient<CheckPublicHolidayRequest> holidayRequestClient,
        IRequestClient<CheckLocationClosedRequest> locationClosedRequestClient,
        IRequestClient<GetContractShiftSchedulesRequest> schedulesRequestClient,
        IPublishEndpoint publishEndpoint)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _holidayRequestClient = holidayRequestClient;
        _locationClosedRequestClient = locationClosedRequestClient;
        _schedulesRequestClient = schedulesRequestClient;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<GenerateShiftsResult> Handle(GenerateShiftsCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting shift generation for Contract {ContractId}", command.ContractId);

        var generateFrom = command.GenerateFromDate ?? DateTime.UtcNow.Date;
        var generateTo = generateFrom.AddDays(command.GenerateDays);

        var createdShiftIds = new List<Guid>();
        var skipReasons = new List<SkipReason>();
        var errors = new List<string>();

        try
        {
            // ================================================================
            // BƯỚC 1: LẤY THÔNG TIN CONTRACT VÀ SHIFT SCHEDULES TỪ CONTRACTS.API
            // ================================================================
            _logger.LogInformation("Fetching shift schedules from Contracts.API for Contract {ContractId}", command.ContractId);

            var schedulesResponse = await _schedulesRequestClient.GetResponse<GetContractShiftSchedulesResponse>(
                new GetContractShiftSchedulesRequest { ContractId = command.ContractId },
                cancellationToken,
                timeout: RequestTimeout.After(s: 10)
            );

            var contractData = schedulesResponse.Message;

            if (contractData.Schedules == null || !contractData.Schedules.Any())
            {
                _logger.LogWarning("No shift schedules found for Contract {ContractId}", command.ContractId);
                return new GenerateShiftsResult
                {
                    ShiftsCreatedCount = 0,
                    ShiftsSkippedCount = 0,
                    GeneratedFrom = generateFrom,
                    GeneratedTo = generateTo,
                    Errors = new List<string> { "Hợp đồng không có mẫu ca nào để tạo" }
                };
            }

            _logger.LogInformation(
                "Found {ScheduleCount} shift schedules for {LocationCount} locations",
                contractData.Schedules.Count,
                contractData.Locations.Count);

            // ================================================================
            // BƯỚC 2: LẶP QUA TỪNG NGÀY TRONG KHOẢNG THỜI GIAN
            // ================================================================
            using var connection = await _dbFactory.CreateConnectionAsync();

            for (var date = generateFrom; date < generateTo; date = date.AddDays(1))
            {
                // ================================================================
                // BƯỚC 2.1: KIỂM TRA NGÀY LỄ
                // ================================================================
                var isHoliday = false;
                string? holidayName = null;

                try
                {
                    var holidayResponse = await _holidayRequestClient.GetResponse<CheckPublicHolidayResponse>(
                        new CheckPublicHolidayRequest { Date = date },
                        cancellationToken,
                        timeout: RequestTimeout.After(s: 5)
                    );

                    isHoliday = holidayResponse.Message.IsHoliday;
                    holidayName = holidayResponse.Message.HolidayName;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check holiday for date {Date}", date);
                }

                // ================================================================
                // BƯỚC 2.2: LẶP QUA TỪNG SHIFT SCHEDULE
                // ================================================================
                foreach (var schedule in contractData.Schedules)
                {
                    try
                    {
                        // Kiểm tra schedule có hiệu lực không
                        if (date < schedule.EffectiveFrom || (schedule.EffectiveTo.HasValue && date > schedule.EffectiveTo.Value))
                        {
                            continue;
                        }

                        // Kiểm tra ngày trong tuần
                        var dayOfWeek = (int)date.DayOfWeek; // 0=Sunday, 1=Monday...
                        if (!ShouldApplyOnDayOfWeek(schedule, dayOfWeek))
                        {
                            continue;
                        }

                        // Kiểm tra ngày lễ
                        if (isHoliday && !schedule.AppliesOnPublicHolidays)
                        {
                            skipReasons.Add(new SkipReason
                            {
                                Date = date,
                                LocationId = schedule.LocationId,
                                LocationName = GetLocationName(contractData.Locations, schedule.LocationId),
                                ScheduleName = schedule.ScheduleName,
                                Reason = $"Ngày lễ: {holidayName} - Schedule không áp dụng vào ngày lễ"
                            });
                            continue;
                        }

                        // Kiểm tra cuối tuần
                        var isWeekend = dayOfWeek == 0 || dayOfWeek == 6; // Sunday or Saturday
                        if (isWeekend && !schedule.AppliesOnWeekends)
                        {
                            skipReasons.Add(new SkipReason
                            {
                                Date = date,
                                LocationId = schedule.LocationId,
                                LocationName = GetLocationName(contractData.Locations, schedule.LocationId),
                                ScheduleName = schedule.ScheduleName,
                                Reason = "Cuối tuần - Schedule không áp dụng vào cuối tuần"
                            });
                            continue;
                        }

                        // ================================================================
                        // BƯỚC 2.3: XÁC ĐỊNH CÁC LOCATION CẦN TẠO CA
                        // ================================================================
                        var locationsToGenerate = new List<LocationInfo>();

                        if (schedule.LocationId.HasValue)
                        {
                            // Schedule cho 1 location cụ thể
                            var location = contractData.Locations.FirstOrDefault(l => l.LocationId == schedule.LocationId.Value);
                            if (location != null)
                            {
                                locationsToGenerate.Add(location);
                            }
                        }
                        else
                        {
                            // Schedule cho tất cả locations
                            locationsToGenerate.AddRange(contractData.Locations);
                        }

                        // ================================================================
                        // BƯỚC 2.4: TẠO SHIFT CHO TỪNG LOCATION
                        // ================================================================
                        foreach (var location in locationsToGenerate)
                        {
                            // Kiểm tra location có đóng cửa không
                            if (schedule.SkipWhenLocationClosed)
                            {
                                try
                                {
                                    var closedResponse = await _locationClosedRequestClient.GetResponse<CheckLocationClosedResponse>(
                                        new CheckLocationClosedRequest
                                        {
                                            LocationId = location.LocationId,
                                            Date = date
                                        },
                                        cancellationToken,
                                        timeout: RequestTimeout.After(s: 5)
                                    );

                                    if (closedResponse.Message.IsClosed)
                                    {
                                        skipReasons.Add(new SkipReason
                                        {
                                            Date = date,
                                            LocationId = location.LocationId,
                                            LocationName = location.LocationName,
                                            ScheduleName = schedule.ScheduleName,
                                            Reason = $"Địa điểm đóng cửa: {closedResponse.Message.Reason}"
                                        });
                                        continue;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to check if location {LocationId} is closed on {Date}",
                                        location.LocationId, date);
                                }
                            }

                            // ================================================================
                            // BƯỚC 2.5: KIỂM TRA CA ĐÃ TỒN TẠI CHƯA (TRÁNH DUPLICATE)
                            // ================================================================
                            var shiftStart = date.Date.Add(schedule.ShiftStartTime);
                            var shiftEnd = date.Date.Add(schedule.ShiftEndTime);

                            if (schedule.CrossesMidnight)
                            {
                                shiftEnd = shiftEnd.AddDays(1);
                            }

                            var existingShift = await connection.QueryFirstOrDefaultAsync<Guid?>(
                                @"SELECT Id FROM shifts
                                  WHERE LocationId = @LocationId
                                    AND ShiftDate = @ShiftDate
                                    AND ShiftStart = @ShiftStart
                                    AND ShiftEnd = @ShiftEnd
                                    AND IsDeleted = 0
                                  LIMIT 1",
                                new { location.LocationId, ShiftDate = date, ShiftStart = shiftStart, ShiftEnd = shiftEnd }
                            );

                            if (existingShift.HasValue)
                            {
                                _logger.LogDebug("Shift already exists for location {LocationId} on {Date} at {Time}",
                                    location.LocationId, date, schedule.ShiftStartTime);
                                continue; // Bỏ qua vì đã tồn tại
                            }

                            // ================================================================
                            // BƯỚC 2.6: TẠO SHIFT MỚI
                            // ================================================================
                            var shift = CreateShiftFromSchedule(
                                schedule,
                                location,
                                date,
                                shiftStart,
                                shiftEnd,
                                command.ContractId,
                                command.CreatedBy
                            );

                            await connection.InsertAsync(shift);
                            createdShiftIds.Add(shift.Id);

                            _logger.LogDebug(
                                "Created shift {ShiftId} for {Location} on {Date} {StartTime}-{EndTime}",
                                shift.Id, location.LocationName, date.ToString("yyyy-MM-dd"),
                                schedule.ShiftStartTime, schedule.ShiftEndTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error generating shift for schedule {ScheduleName} on {Date}",
                            schedule.ScheduleName, date);
                        errors.Add($"Lỗi tạo ca '{schedule.ScheduleName}' ngày {date:yyyy-MM-dd}: {ex.Message}");
                    }
                }
            }

            // ================================================================
            // BƯỚC 3: PUBLISH EVENT VỀ SHIFT GENERATION
            // ================================================================
            if (createdShiftIds.Any())
            {
                await _publishEndpoint.Publish(new ShiftsGeneratedEvent
                {
                    ContractId = command.ContractId,
                    GenerationDate = generateFrom,
                    ShiftsCreatedCount = createdShiftIds.Count,
                    ShiftsSkippedCount = skipReasons.Count,
                    SkipReasons = skipReasons.Select(s => s.Reason).Distinct().ToList(),
                    GeneratedAt = DateTime.UtcNow,
                    GeneratedByJob = command.CreatedBy.HasValue ? $"User_{command.CreatedBy}" : "ContractActivatedEvent"
                }, cancellationToken);

                _logger.LogInformation(
                    "Published ShiftsGeneratedEvent: {CreatedCount} shifts created, {SkippedCount} skipped",
                    createdShiftIds.Count, skipReasons.Count);
            }

            // ================================================================
            // BƯỚC 4: RETURN KẾT QUẢ
            // ================================================================
            var result = new GenerateShiftsResult
            {
                ShiftsCreatedCount = createdShiftIds.Count,
                ShiftsSkippedCount = skipReasons.Count,
                SkipReasons = skipReasons,
                CreatedShiftIds = createdShiftIds,
                Errors = errors,
                GeneratedFrom = generateFrom,
                GeneratedTo = generateTo
            };

            _logger.LogInformation(
                "Shift generation completed for Contract {ContractId}: " +
                "{CreatedCount} created, {SkippedCount} skipped, {ErrorCount} errors",
                command.ContractId, result.ShiftsCreatedCount, result.ShiftsSkippedCount, errors.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during shift generation for Contract {ContractId}", command.ContractId);
            throw;
        }
    }

    // ================================================================
    // HELPER METHODS
    // ================================================================

    /// <summary>
    /// Kiểm tra schedule có áp dụng vào ngày trong tuần không
    /// </summary>
    private bool ShouldApplyOnDayOfWeek(ShiftScheduleInfo schedule, int dayOfWeek)
    {
        return dayOfWeek switch
        {
            0 => schedule.AppliesSunday,     // Sunday
            1 => schedule.AppliesMonday,     // Monday
            2 => schedule.AppliesTuesday,    // Tuesday
            3 => schedule.AppliesWednesday,  // Wednesday
            4 => schedule.AppliesThursday,   // Thursday
            5 => schedule.AppliesFriday,     // Friday
            6 => schedule.AppliesSaturday,   // Saturday
            _ => false
        };
    }

    /// <summary>
    /// Lấy tên location từ danh sách
    /// </summary>
    private string GetLocationName(List<LocationInfo> locations, Guid? locationId)
    {
        if (!locationId.HasValue) return "Tất cả địa điểm";

        var location = locations.FirstOrDefault(l => l.LocationId == locationId.Value);
        return location?.LocationName ?? "Unknown";
    }

    /// <summary>
    /// Tạo shift object từ schedule template
    /// </summary>
    private Models.Shifts CreateShiftFromSchedule(
        ShiftScheduleInfo schedule,
        LocationInfo location,
        DateTime date,
        DateTime shiftStart,
        DateTime shiftEnd,
        Guid contractId,
        Guid? createdBy)
    {
        var totalMinutes = (int)(shiftEnd - shiftStart).TotalMinutes;
        var workMinutes = totalMinutes - schedule.BreakMinutes;
        var dayOfWeek = (int)date.DayOfWeek; // 0=Sunday, 1=Monday...

        var shift = new Models.Shifts
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            LocationId = location.LocationId,
            ShiftTemplateId = null, // Không dùng template từ Shifts.API, dùng schedule từ Contracts.API

            // Date splitting
            ShiftDate = date,
            ShiftDay = date.Day,
            ShiftMonth = date.Month,
            ShiftYear = date.Year,
            ShiftQuarter = (date.Month - 1) / 3 + 1,
            ShiftWeek = GetIso8601WeekOfYear(date),
            DayOfWeek = dayOfWeek == 0 ? 7 : dayOfWeek, // 1=Mon, 7=Sun

            // Time
            ShiftStart = shiftStart,
            ShiftEnd = shiftEnd,
            ShiftEndDate = schedule.CrossesMidnight ? shiftEnd.Date : date,

            // Duration
            TotalDurationMinutes = totalMinutes,
            WorkDurationMinutes = workMinutes,
            WorkDurationHours = Math.Round((decimal)workMinutes / 60, 2),
            BreakDurationMinutes = schedule.BreakMinutes,
            PaidBreakMinutes = 0,
            UnpaidBreakMinutes = schedule.BreakMinutes,

            // Staffing
            RequiredGuards = schedule.GuardsPerShift,
            AssignedGuardsCount = 0,
            ConfirmedGuardsCount = 0,
            CheckedInGuardsCount = 0,
            CompletedGuardsCount = 0,
            IsFullyStaffed = false,
            IsUnderstaffed = true,
            IsOverstaffed = false,
            StaffingPercentage = 0,

            // Day classification
            IsRegularWeekday = dayOfWeek >= 1 && dayOfWeek <= 5,
            IsSaturday = dayOfWeek == 6,
            IsSunday = dayOfWeek == 0,
            IsPublicHoliday = false, // Sẽ được update sau nếu cần
            IsTetHoliday = false,

            // Night shift classification
            IsNightShift = IsNightShift(schedule.ShiftStartTime, schedule.ShiftEndTime),
            NightHours = CalculateNightHours(shiftStart, shiftEnd),
            DayHours = CalculateDayHours(shiftStart, shiftEnd),

            // Shift type
            ShiftType = schedule.ScheduleType.ToUpper(),

            // Special flags
            IsMandatory = false,
            IsCritical = false,
            IsTrainingShift = schedule.ScheduleType.ToUpper() == "TRAINING",
            RequiresArmedGuard = schedule.RequiresArmedGuard,

            // Approval workflow
            RequiresApproval = false,
            ApprovalStatus = "APPROVED", // Auto-approved vì từ contract

            // Status
            Status = "SCHEDULED", // DRAFT | SCHEDULED | IN_PROGRESS | COMPLETED | CANCELLED

            // Description (optional)
            Description = $"Auto-generated from schedule: {schedule.ScheduleName}",

            // Metadata
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy ?? Guid.Empty,
            IsDeleted = false,
            Version = 1
        };

        return shift;
    }

    /// <summary>
    /// Kiểm tra ca đêm (22:00-06:00)
    /// </summary>
    private bool IsNightShift(TimeSpan startTime, TimeSpan endTime)
    {
        var nightStart = new TimeSpan(22, 0, 0);
        var nightEnd = new TimeSpan(6, 0, 0);

        return startTime >= nightStart || endTime <= nightEnd;
    }

    /// <summary>
    /// Tính tuần ISO 8601 trong năm
    /// </summary>
    private int GetIso8601WeekOfYear(DateTime date)
    {
        var day = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
        if (day >= System.DayOfWeek.Monday && day <= System.DayOfWeek.Wednesday)
        {
            date = date.AddDays(3);
        }

        return System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            date,
            System.Globalization.CalendarWeekRule.FirstFourDayWeek,
            System.DayOfWeek.Monday);
    }

    /// <summary>
    /// Tính số giờ làm việc ban đêm (22:00-06:00)
    /// Night hours calculation for overtime pay
    /// </summary>
    private decimal CalculateNightHours(DateTime shiftStart, DateTime shiftEnd)
    {
        var nightStart = new TimeSpan(22, 0, 0); // 22:00
        var nightEnd = new TimeSpan(6, 0, 0);    // 06:00

        var totalNightMinutes = 0m;
        var current = shiftStart;

        while (current < shiftEnd)
        {
            var currentTime = current.TimeOfDay;

            // Kiểm tra nếu thời gian hiện tại nằm trong khoảng đêm
            // Đêm là từ 22:00 đến 06:00 sáng hôm sau
            if (currentTime >= nightStart || currentTime < nightEnd)
            {
                totalNightMinutes++;
            }

            current = current.AddMinutes(1);
        }

        return Math.Round(totalNightMinutes / 60, 2);
    }

    /// <summary>
    /// Tính số giờ làm việc ban ngày (06:00-22:00)
    /// Day hours calculation
    /// </summary>
    private decimal CalculateDayHours(DateTime shiftStart, DateTime shiftEnd)
    {
        var totalMinutes = (decimal)(shiftEnd - shiftStart).TotalMinutes;
        var nightMinutes = CalculateNightHours(shiftStart, shiftEnd) * 60;
        var dayMinutes = totalMinutes - nightMinutes;

        return Math.Round(dayMinutes / 60, 2);
    }
}
