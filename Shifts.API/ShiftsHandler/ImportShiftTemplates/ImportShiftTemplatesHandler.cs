using BuildingBlocks.CQRS;
using BuildingBlocks.Messaging.Events;
using Dapper;
using Dapper.Contrib.Extensions;
using Shifts.API.Data;
using Shifts.API.Models;

namespace Shifts.API.ShiftsHandler.ImportShiftTemplates;

/// <summary>
/// Handler import ShiftTemplates từ Contract Shift Schedules
/// CRITICAL: Validation thời gian cực kỳ chặt chẽ để tránh conflict và bug logic
/// </summary>
public class ImportShiftTemplatesHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ImportShiftTemplatesHandler> logger)
    : ICommandHandler<ImportShiftTemplatesCommand, ImportShiftTemplatesResult>
{
    public async Task<ImportShiftTemplatesResult> Handle(
        ImportShiftTemplatesCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Importing {Count} shift templates from Contract {ContractNumber}",
            request.ShiftSchedules.Count,
            request.ContractNumber);

        var createdTemplateIds = new List<Guid>();
        var importDetails = new List<TemplateImportInfo>();
        var errors = new List<string>();
        int createdCount = 0, updatedCount = 0, skippedCount = 0;

        using var connection = await connectionFactory.CreateConnectionAsync();

        foreach (var schedule in request.ShiftSchedules)
        {
            try
            {
                // ================================================================
                // BƯỚC 1: VALIDATION THỜI GIAN CHẶT CHẼ
                // ================================================================
                var timeValidation = ValidateShiftTime(schedule);

                if (!timeValidation.IsValid)
                {
                    logger.LogError(
                        "Time validation failed for schedule {ScheduleName}: {Errors}",
                        schedule.ScheduleName,
                        string.Join(", ", timeValidation.Errors));

                    errors.AddRange(timeValidation.Errors);
                    skippedCount++;

                    importDetails.Add(new TemplateImportInfo
                    {
                        TemplateId = Guid.Empty,
                        TemplateCode = GenerateTemplateCode(schedule),
                        TemplateName = schedule.ScheduleName,
                        Action = "Skipped",
                        Reason = $"Time validation failed: {string.Join(", ", timeValidation.Errors)}",
                        TimeValidation = timeValidation
                    });

                    continue;
                }

                // Log warnings nếu có
                if (timeValidation.Warnings.Any())
                {
                    logger.LogWarning(
                        "Time warnings for schedule {ScheduleName}: {Warnings}",
                        schedule.ScheduleName,
                        string.Join(", ", timeValidation.Warnings));
                }

                // ================================================================
                // BƯỚC 2: CHECK TEMPLATE ĐÃ TỒN TẠI CHƯA
                // ================================================================
                var templateCode = GenerateTemplateCode(schedule);

                var existingTemplate = await connection.QueryFirstOrDefaultAsync<ShiftTemplates>(
                    "SELECT * FROM shift_templates WHERE TemplateCode = @TemplateCode AND IsDeleted = 0",
                    new { TemplateCode = templateCode });

                // ================================================================
                // BƯỚC 3: CREATE HOẶC UPDATE TEMPLATE
                // ================================================================
                ShiftTemplates template;
                string action;

                if (existingTemplate == null)
                {
                    // CREATE NEW TEMPLATE
                    // Find matching location for this schedule
                    var location = request.Locations.FirstOrDefault(l => l.LocationId == schedule.LocationId);
                    template = CreateShiftTemplate(schedule, request, timeValidation, templateCode, location);
                    await connection.InsertAsync(template);
                    createdTemplateIds.Add(template.Id);
                    createdCount++;
                    action = "Created";

                    logger.LogInformation(
                        "✓ Created template {TemplateCode}: {TemplateName} | {StartTime}-{EndTime} | Guards: {Guards} | Duration: {Duration}h",
                        template.TemplateCode,
                        template.TemplateName,
                        template.StartTime,
                        template.EndTime,
                        template.MinGuardsRequired,
                        template.DurationHours);
                }
                else
                {
                    // UPDATE EXISTING TEMPLATE
                    template = UpdateShiftTemplate(existingTemplate, schedule, request, timeValidation);
                    await connection.UpdateAsync(template);
                    updatedCount++;
                    action = "Updated";

                    logger.LogInformation(
                        "✓ Updated template {TemplateCode}: {TemplateName}",
                        template.TemplateCode,
                        template.TemplateName);
                }

                // ================================================================
                // BƯỚC 4: GHI LOG CHI TIẾT
                // ================================================================
                importDetails.Add(new TemplateImportInfo
                {
                    TemplateId = template.Id,
                    TemplateCode = template.TemplateCode,
                    TemplateName = template.TemplateName,
                    Action = action,
                    Reason = action == "Created" ? "New template from contract" : "Existing template updated",
                    TimeValidation = timeValidation
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to import schedule {ScheduleName} from Contract {ContractNumber}",
                    schedule.ScheduleName,
                    request.ContractNumber);

                errors.Add($"Schedule '{schedule.ScheduleName}': {ex.Message}");
                skippedCount++;
            }
        }

        // ================================================================
        // TỔNG HỢP KẾT QUẢ
        // ================================================================
        logger.LogInformation(
            @"✓ Import completed for Contract {ContractNumber}:
              - Created: {Created} templates
              - Updated: {Updated} templates
              - Skipped: {Skipped} templates
              - Errors: {Errors}",
            request.ContractNumber,
            createdCount,
            updatedCount,
            skippedCount,
            errors.Count);

        return new ImportShiftTemplatesResult
        {
            Success = errors.Count == 0 || createdCount > 0,
            TemplatesCreatedCount = createdCount,
            TemplatesUpdatedCount = updatedCount,
            TemplatesSkippedCount = skippedCount,
            CreatedTemplateIds = createdTemplateIds,
            Errors = errors,
            ImportDetails = importDetails
        };
    }

    /// <summary>
    /// VALIDATION THỜI GIAN CỰC KỲ CHẶT CHẼ
    /// Kiểm tra mọi khía cạnh về thời gian để tránh conflict
    /// </summary>
    private TimeValidationResult ValidateShiftTime(ContractShiftScheduleDto schedule)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // ================================================================
        // 1. VALIDATE TIMESPAN RANGE (00:00:00 đến 23:59:59)
        // ================================================================
        if (schedule.ShiftStartTime < TimeSpan.Zero || schedule.ShiftStartTime >= TimeSpan.FromDays(1))
        {
            errors.Add($"Invalid ShiftStartTime: {schedule.ShiftStartTime}. Must be between 00:00:00 and 23:59:59");
        }

        if (schedule.ShiftEndTime < TimeSpan.Zero || schedule.ShiftEndTime >= TimeSpan.FromDays(1))
        {
            errors.Add($"Invalid ShiftEndTime: {schedule.ShiftEndTime}. Must be between 00:00:00 and 23:59:59");
        }

        // Stop early if basic validation fails
        if (errors.Any())
        {
            return new TimeValidationResult
            {
                IsValid = false,
                Errors = errors,
                Warnings = warnings
            };
        }

        // ================================================================
        // 2. CALCULATE DURATION & CHECK MIDNIGHT CROSSING
        // ================================================================
        bool crossesMidnight = schedule.ShiftEndTime <= schedule.ShiftStartTime;
        decimal actualDurationHours;

        if (crossesMidnight)
        {
            // Ca qua nửa đêm: 22:00 → 06:00 = (24:00 - 22:00) + 06:00 = 8h
            var hoursUntilMidnight = 24 - schedule.ShiftStartTime.TotalHours;
            var hoursAfterMidnight = schedule.ShiftEndTime.TotalHours;
            actualDurationHours = (decimal)(hoursUntilMidnight + hoursAfterMidnight);
        }
        else
        {
            // Ca trong ngày: 08:00 → 17:00 = 9h
            actualDurationHours = (decimal)(schedule.ShiftEndTime - schedule.ShiftStartTime).TotalHours;
        }

        // ================================================================
        // 3. VALIDATE DURATION CONSISTENCY
        // ================================================================
        var declaredDurationHours = schedule.DurationHours;
        var durationDiff = Math.Abs(actualDurationHours - declaredDurationHours);

        // Cho phép sai số 0.1h (6 phút) do làm tròn
        bool durationMatches = durationDiff <= 0.1m;

        if (!durationMatches)
        {
            errors.Add(
                $"Duration mismatch: Declared {declaredDurationHours}h but calculated {actualDurationHours}h " +
                $"from {schedule.ShiftStartTime} to {schedule.ShiftEndTime}. Difference: {durationDiff:F2}h");
        }

        // ================================================================
        // 4. VALIDATE CROSSES MIDNIGHT FLAG
        // ================================================================
        if (schedule.CrossesMidnight != crossesMidnight)
        {
            warnings.Add(
                $"CrossesMidnight flag mismatch: Declared {schedule.CrossesMidnight} but calculated {crossesMidnight}. " +
                $"Will use calculated value.");
        }

        // ================================================================
        // 5. VALIDATE MINIMUM DURATION (tối thiểu 1 giờ)
        // ================================================================
        if (actualDurationHours < 1.0m)
        {
            errors.Add($"Shift duration too short: {actualDurationHours:F2}h. Minimum is 1 hour.");
        }

        // ================================================================
        // 6. VALIDATE MAXIMUM DURATION (tối đa 24 giờ)
        // ================================================================
        if (actualDurationHours > 24.0m)
        {
            errors.Add($"Shift duration too long: {actualDurationHours:F2}h. Maximum is 24 hours.");
        }

        // ================================================================
        // 7. CHECK VIETNAMESE LABOR LAW (max 12h/shift in security)
        // ================================================================
        if (actualDurationHours > 12.0m)
        {
            warnings.Add(
                $"Shift duration {actualDurationHours:F2}h exceeds recommended 12h per shift. " +
                $"Ensure compliance with labor laws.");
        }

        // ================================================================
        // 8. VALIDATE BREAK TIME
        // ================================================================
        if (schedule.BreakMinutes < 0)
        {
            errors.Add($"Invalid BreakMinutes: {schedule.BreakMinutes}. Cannot be negative.");
        }

        if (schedule.BreakMinutes > (int)(actualDurationHours * 60))
        {
            errors.Add(
                $"Break time {schedule.BreakMinutes} minutes exceeds shift duration " +
                $"{(int)(actualDurationHours * 60)} minutes.");
        }

        // Cảnh báo nếu ca dài mà không có break
        if (actualDurationHours >= 6.0m && schedule.BreakMinutes == 0)
        {
            warnings.Add(
                $"Shift duration {actualDurationHours:F2}h has no break time. " +
                $"Vietnamese labor law requires break for shifts >= 6h.");
        }

        // ================================================================
        // 9. DETECT NIGHT SHIFT (22:00-06:00)
        // ================================================================
        bool isNightShift = IsNightShift(schedule.ShiftStartTime, schedule.ShiftEndTime, crossesMidnight);

        // ================================================================
        // 10. VALIDATE AT LEAST ONE DAY SELECTED
        // ================================================================
        bool hasAnyDaySelected = schedule.AppliesMonday || schedule.AppliesTuesday ||
                                 schedule.AppliesWednesday || schedule.AppliesThursday ||
                                 schedule.AppliesFriday || schedule.AppliesSaturday ||
                                 schedule.AppliesSunday;

        if (!hasAnyDaySelected)
        {
            errors.Add("No days of week selected. Template must apply to at least one day.");
        }

        // ================================================================
        // 11. VALIDATE GUARDS PER SHIFT
        // ================================================================
        if (schedule.GuardsPerShift <= 0)
        {
            errors.Add($"Invalid GuardsPerShift: {schedule.GuardsPerShift}. Must be at least 1.");
        }

        if (schedule.GuardsPerShift > 50)
        {
            warnings.Add($"GuardsPerShift {schedule.GuardsPerShift} seems unusually high. Please verify.");
        }

        // ================================================================
        // 12. VALIDATE EFFECTIVE DATES
        // ================================================================
        if (schedule.EffectiveTo.HasValue && schedule.EffectiveTo.Value < schedule.EffectiveFrom)
        {
            errors.Add(
                $"EffectiveTo {schedule.EffectiveTo.Value:yyyy-MM-dd} is before " +
                $"EffectiveFrom {schedule.EffectiveFrom:yyyy-MM-dd}");
        }

        // ================================================================
        // RETURN VALIDATION RESULT
        // ================================================================
        return new TimeValidationResult
        {
            IsValid = errors.Count == 0,
            CrossesMidnight = crossesMidnight,
            ActualDurationHours = actualDurationHours,
            DeclaredDurationHours = declaredDurationHours,
            DurationMatches = durationMatches,
            IsNightShift = isNightShift,
            Warnings = warnings,
            Errors = errors
        };
    }

    /// <summary>
    /// Check if shift is night shift (22:00-06:00)
    /// Vietnam: Ca đêm = 22h-6h sáng hôm sau → +30% lương
    /// </summary>
    private bool IsNightShift(TimeSpan startTime, TimeSpan endTime, bool crossesMidnight)
    {
        var nightStart = TimeSpan.FromHours(22); // 22:00
        var nightEnd = TimeSpan.FromHours(6);    // 06:00

        if (crossesMidnight)
        {
            // Ca qua đêm: 22:00 → 06:00 hoặc 23:00 → 07:00
            // Nếu start >= 22:00 hoặc end <= 06:00 → night shift
            return startTime >= nightStart || endTime <= nightEnd;
        }
        else
        {
            // Ca trong ngày: không phải night shift UNLESS toàn bộ ca trong khoảng 00:00-06:00
            return startTime >= TimeSpan.Zero && endTime <= nightEnd;
        }
    }

    /// <summary>
    /// Generate unique template code
    /// Format: CONTRACT-{ContractNumber}-{ScheduleName}-{Time}
    /// Example: CONTRACT-CTR2025001-MORNING-0800-1700
    /// </summary>
    private string GenerateTemplateCode(ContractShiftScheduleDto schedule)
    {
        var startHour = schedule.ShiftStartTime.ToString(@"hhmm");
        var endHour = schedule.ShiftEndTime.ToString(@"hhmm");

        // Sanitize schedule name (remove special chars)
        var safeName = new string(schedule.ScheduleName
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
            .ToArray())
            .ToUpper();

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "SCHEDULE";
        }

        return $"{safeName}-{startHour}-{endHour}";
    }

    /// <summary>
    /// Create new ShiftTemplate from schedule with location info
    /// </summary>
    private ShiftTemplates CreateShiftTemplate(
        ContractShiftScheduleDto schedule,
        ImportShiftTemplatesCommand request,
        TimeValidationResult validation,
        string templateCode,
        ContractLocationDto? location = null)
    {
        return new ShiftTemplates
        {
            Id = Guid.NewGuid(),
            ContractId = request.ContractId, // ✨ Set ContractId from event
            TemplateCode = templateCode,
            TemplateName = schedule.ScheduleName,
            Description = $"Imported from Contract {request.ContractNumber}",

            // TIME - Sử dụng giá trị đã validate
            StartTime = schedule.ShiftStartTime,
            EndTime = schedule.ShiftEndTime,
            DurationHours = validation.ActualDurationHours, // Sử dụng calculated value
            BreakDurationMinutes = schedule.BreakMinutes,
            PaidBreakMinutes = 0, // Default
            UnpaidBreakMinutes = schedule.BreakMinutes,

            // CLASSIFICATION - Sử dụng giá trị đã validate
            IsNightShift = validation.IsNightShift,
            IsOvernight = validation.CrossesMidnight,
            CrossesMidnight = validation.CrossesMidnight,

            // DAY OF WEEK
            AppliesMonday = schedule.AppliesMonday,
            AppliesTuesday = schedule.AppliesTuesday,
            AppliesWednesday = schedule.AppliesWednesday,
            AppliesThursday = schedule.AppliesThursday,
            AppliesFriday = schedule.AppliesFriday,
            AppliesSaturday = schedule.AppliesSaturday,
            AppliesSunday = schedule.AppliesSunday,

            // STAFFING
            MinGuardsRequired = schedule.GuardsPerShift,
            MaxGuardsAllowed = schedule.GuardsPerShift, // Same as min for now
            OptimalGuards = schedule.GuardsPerShift,

            // LOCATION INFO (Cached from contract)
            LocationId = location?.LocationId,
            LocationName = location?.LocationName,
            LocationAddress = location?.LocationAddress,
            LocationLatitude = location?.Latitude,
            LocationLongitude = location?.Longitude,

            // VALIDITY
            EffectiveFrom = schedule.EffectiveFrom,
            EffectiveTo = schedule.EffectiveTo,
            IsActive = true,

            // AUDIT
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.ImportedBy,
            IsDeleted = false
        };
    }

    /// <summary>
    /// Update existing template with new schedule data
    /// </summary>
    private ShiftTemplates UpdateShiftTemplate(
        ShiftTemplates existing,
        ContractShiftScheduleDto schedule,
        ImportShiftTemplatesCommand request,
        TimeValidationResult validation)
    {
        // Update ContractId (in case it changed)
        existing.ContractId = request.ContractId;

        // Update time fields
        existing.StartTime = schedule.ShiftStartTime;
        existing.EndTime = schedule.ShiftEndTime;
        existing.DurationHours = validation.ActualDurationHours;
        existing.BreakDurationMinutes = schedule.BreakMinutes;
        existing.UnpaidBreakMinutes = schedule.BreakMinutes;

        // Update classification
        existing.IsNightShift = validation.IsNightShift;
        existing.IsOvernight = validation.CrossesMidnight;
        existing.CrossesMidnight = validation.CrossesMidnight;

        // Update days
        existing.AppliesMonday = schedule.AppliesMonday;
        existing.AppliesTuesday = schedule.AppliesTuesday;
        existing.AppliesWednesday = schedule.AppliesWednesday;
        existing.AppliesThursday = schedule.AppliesThursday;
        existing.AppliesFriday = schedule.AppliesFriday;
        existing.AppliesSaturday = schedule.AppliesSaturday;
        existing.AppliesSunday = schedule.AppliesSunday;

        // Update staffing
        existing.MinGuardsRequired = schedule.GuardsPerShift;
        existing.MaxGuardsAllowed = schedule.GuardsPerShift;
        existing.OptimalGuards = schedule.GuardsPerShift;

        // Update validity
        existing.EffectiveFrom = schedule.EffectiveFrom;
        existing.EffectiveTo = schedule.EffectiveTo;

        // Update audit
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = request.ImportedBy;

        return existing;
    }
}