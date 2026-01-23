namespace Shifts.API.ShiftsHandler.ImportShiftTemplates;

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
                
                if (timeValidation.Warnings.Any())
                {
                    logger.LogWarning(
                        "Time warnings for schedule {ScheduleName}: {Warnings}",
                        schedule.ScheduleName,
                        string.Join(", ", timeValidation.Warnings));
                }
                
                var templateCode = GenerateTemplateCode(schedule);

                var existingTemplate = await connection.QueryFirstOrDefaultAsync<ShiftTemplates>(
                    "SELECT * FROM shift_templates WHERE TemplateCode = @TemplateCode AND IsDeleted = 0",
                    new { TemplateCode = templateCode });
                
                ShiftTemplates template;
                string action;

                if (existingTemplate == null)
                {
                    var location = request.Locations.FirstOrDefault(l => l.LocationId == schedule.LocationId);
                    template = CreateShiftTemplate(schedule, request, timeValidation, templateCode, location);
                    await connection.InsertAsync(template);
                    createdTemplateIds.Add(template.Id);
                    createdCount++;
                    action = "Created";

                    logger.LogInformation(
                        "Created template {TemplateCode}: {TemplateName} | {StartTime}-{EndTime} | Guards: {Guards} | Duration: {Duration}h",
                        template.TemplateCode,
                        template.TemplateName,
                        template.StartTime,
                        template.EndTime,
                        template.MinGuardsRequired,
                        template.DurationHours);
                }
                else
                {
                    template = UpdateShiftTemplate(existingTemplate, schedule, request, timeValidation);
                    await connection.UpdateAsync(template);
                    updatedCount++;
                    action = "Updated";

                    logger.LogInformation(
                        "Updated template {TemplateCode}: {TemplateName}",
                        template.TemplateCode,
                        template.TemplateName);
                }
                
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

        logger.LogInformation(
            @"Import completed for Contract {ContractNumber}:
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
    
    private TimeValidationResult ValidateShiftTime(ContractShiftScheduleDto schedule)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        
        if (schedule.ShiftStartTime < TimeSpan.Zero || schedule.ShiftStartTime >= TimeSpan.FromDays(1))
        {
            errors.Add($"Invalid ShiftStartTime: {schedule.ShiftStartTime}. Must be between 00:00:00 and 23:59:59");
        }

        if (schedule.ShiftEndTime < TimeSpan.Zero || schedule.ShiftEndTime >= TimeSpan.FromDays(1))
        {
            errors.Add($"Invalid ShiftEndTime: {schedule.ShiftEndTime}. Must be between 00:00:00 and 23:59:59");
        }
        if (errors.Any())
        {
            return new TimeValidationResult
            {
                IsValid = false,
                Errors = errors,
                Warnings = warnings
            };
        }

        bool crossesMidnight = schedule.ShiftEndTime <= schedule.ShiftStartTime;
        decimal actualDurationHours;

        if (crossesMidnight)
        {
            var hoursUntilMidnight = 24 - schedule.ShiftStartTime.TotalHours;
            var hoursAfterMidnight = schedule.ShiftEndTime.TotalHours;
            actualDurationHours = (decimal)(hoursUntilMidnight + hoursAfterMidnight);
        }
        else
        {
            actualDurationHours = (decimal)(schedule.ShiftEndTime - schedule.ShiftStartTime).TotalHours;
        }
        var declaredDurationHours = schedule.DurationHours;
        var durationDiff = Math.Abs(actualDurationHours - declaredDurationHours);
        
        bool durationMatches = durationDiff <= 0.1m;

        if (!durationMatches)
        {
            errors.Add(
                $"Duration mismatch: Declared {declaredDurationHours}h but calculated {actualDurationHours}h " +
                $"from {schedule.ShiftStartTime} to {schedule.ShiftEndTime}. Difference: {durationDiff:F2}h");
        }

        if (schedule.CrossesMidnight != crossesMidnight)
        {
            warnings.Add(
                $"CrossesMidnight flag mismatch: Declared {schedule.CrossesMidnight} but calculated {crossesMidnight}. " +
                $"Will use calculated value.");
        }

        if (actualDurationHours < 1.0m)
        {
            errors.Add($"Shift duration too short: {actualDurationHours:F2}h. Minimum is 1 hour.");
        }
        
        if (actualDurationHours > 24.0m)
        {
            errors.Add($"Shift duration too long: {actualDurationHours:F2}h. Maximum is 24 hours.");
        }

        if (actualDurationHours > 12.0m)
        {
            warnings.Add(
                $"Shift duration {actualDurationHours:F2}h exceeds recommended 12h per shift. " +
                $"Ensure compliance with labor laws.");
        }

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

        if (actualDurationHours >= 6.0m && schedule.BreakMinutes == 0)
        {
            warnings.Add(
                $"Shift duration {actualDurationHours:F2}h has no break time. " +
                $"Vietnamese labor law requires break for shifts >= 6h.");
        }
        
        bool isNightShift = IsNightShift(schedule.ShiftStartTime, schedule.ShiftEndTime, crossesMidnight);

        bool hasAnyDaySelected = schedule.AppliesMonday || schedule.AppliesTuesday ||
                                 schedule.AppliesWednesday || schedule.AppliesThursday ||
                                 schedule.AppliesFriday || schedule.AppliesSaturday ||
                                 schedule.AppliesSunday;

        if (!hasAnyDaySelected)
        {
            errors.Add("No days of week selected. Template must apply to at least one day.");
        }

        if (schedule.GuardsPerShift <= 0)
        {
            errors.Add($"Invalid GuardsPerShift: {schedule.GuardsPerShift}. Must be at least 1.");
        }

        if (schedule.GuardsPerShift > 50)
        {
            warnings.Add($"GuardsPerShift {schedule.GuardsPerShift} seems unusually high. Please verify.");
        }

        var today = DateTime.UtcNow.Date;
        if (schedule.EffectiveFrom.Date <= today)
        {
            errors.Add(
                $"EffectiveFrom {schedule.EffectiveFrom:yyyy-MM-dd} phải sau ngày hôm nay ({today:yyyy-MM-dd}). " +
                $"Không thể tạo shift template với ngày bắt đầu trong quá khứ hoặc hôm nay.");
        }

        if (schedule.EffectiveTo.HasValue && schedule.EffectiveTo.Value < schedule.EffectiveFrom)
        {
            errors.Add(
                $"EffectiveTo {schedule.EffectiveTo.Value:yyyy-MM-dd} is before " +
                $"EffectiveFrom {schedule.EffectiveFrom:yyyy-MM-dd}");
        }

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
    
    private bool IsNightShift(TimeSpan startTime, TimeSpan endTime, bool crossesMidnight)
    {
        var nightStart = TimeSpan.FromHours(22); 
        var nightEnd = TimeSpan.FromHours(6);   

        if (crossesMidnight)
        {

            return startTime >= nightStart || endTime <= nightEnd;
        }
        else
        {
            return startTime >= TimeSpan.Zero && endTime <= nightEnd;
        }
    }

    private string GenerateTemplateCode(ContractShiftScheduleDto schedule)
    {
        var startHour = schedule.ShiftStartTime.ToString(@"hhmm");
        var endHour = schedule.ShiftEndTime.ToString(@"hhmm");
        
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
            ContractId = request.ContractId,
            ManagerId = request.ManagerId, 
            TemplateCode = templateCode,
            TemplateName = schedule.ScheduleName,
            Description = $"Imported from Contract {request.ContractNumber}",
            StartTime = schedule.ShiftStartTime,
            EndTime = schedule.ShiftEndTime,
            DurationHours = validation.ActualDurationHours,
            BreakDurationMinutes = schedule.BreakMinutes,
            PaidBreakMinutes = 0,
            UnpaidBreakMinutes = schedule.BreakMinutes,
            IsNightShift = validation.IsNightShift,
            IsOvernight = validation.CrossesMidnight,
            CrossesMidnight = validation.CrossesMidnight,
            AppliesMonday = schedule.AppliesMonday,
            AppliesTuesday = schedule.AppliesTuesday,
            AppliesWednesday = schedule.AppliesWednesday,
            AppliesThursday = schedule.AppliesThursday,
            AppliesFriday = schedule.AppliesFriday,
            AppliesSaturday = schedule.AppliesSaturday,
            AppliesSunday = schedule.AppliesSunday,
            MinGuardsRequired = schedule.GuardsPerShift,
            MaxGuardsAllowed = schedule.GuardsPerShift, 
            OptimalGuards = schedule.GuardsPerShift,
            LocationId = location?.LocationId,
            LocationName = location?.LocationName,
            LocationAddress = location?.LocationAddress,
            LocationLatitude = location?.Latitude,
            LocationLongitude = location?.Longitude,
            EffectiveFrom = schedule.EffectiveFrom,
            EffectiveTo = schedule.EffectiveTo,
            IsActive = true,
            Status = "await_create_shift",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.ImportedBy,
            IsDeleted = false
        };
    }
    
    private ShiftTemplates UpdateShiftTemplate(
        ShiftTemplates existing,
        ContractShiftScheduleDto schedule,
        ImportShiftTemplatesCommand request,
        TimeValidationResult validation)
    {
        existing.ContractId = request.ContractId;
        existing.StartTime = schedule.ShiftStartTime;
        existing.EndTime = schedule.ShiftEndTime;
        existing.DurationHours = validation.ActualDurationHours;
        existing.BreakDurationMinutes = schedule.BreakMinutes;
        existing.UnpaidBreakMinutes = schedule.BreakMinutes;
        existing.IsNightShift = validation.IsNightShift;
        existing.IsOvernight = validation.CrossesMidnight;
        existing.CrossesMidnight = validation.CrossesMidnight;
        existing.AppliesMonday = schedule.AppliesMonday;
        existing.AppliesTuesday = schedule.AppliesTuesday;
        existing.AppliesWednesday = schedule.AppliesWednesday;
        existing.AppliesThursday = schedule.AppliesThursday;
        existing.AppliesFriday = schedule.AppliesFriday;
        existing.AppliesSaturday = schedule.AppliesSaturday;
        existing.AppliesSunday = schedule.AppliesSunday;
        existing.MinGuardsRequired = schedule.GuardsPerShift;
        existing.MaxGuardsAllowed = schedule.GuardsPerShift;
        existing.OptimalGuards = schedule.GuardsPerShift;
        existing.EffectiveFrom = schedule.EffectiveFrom;
        existing.EffectiveTo = schedule.EffectiveTo;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = request.ImportedBy;

        // Reset status to allow manager to create new shifts for updated template
        existing.Status = "await_create_shift";

        return existing;
    }
}