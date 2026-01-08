using Shifts.API.ShiftsHandler.AssignTeamToShift;

namespace Shifts.API.ShiftsHandler.GenerateShifts;

public class GenerateShiftsHandler(
    IDbConnectionFactory connectionFactory,
    IRequestClient<BatchCheckPublicHolidaysRequest> batchHolidayClient,
    IPublishEndpoint publishEndpoint,
    ISender sender,
    ILogger<GenerateShiftsHandler> logger)
    : ICommandHandler<GenerateShiftsCommand, GenerateShiftsResult>
{
    public async Task<GenerateShiftsResult> Handle(
        GenerateShiftsCommand command,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        logger.LogInformation(
            "Starting shift generation from {TemplateCount} templates by Manager {ManagerId}",
            command.ShiftTemplateIds.Count,
            command.ManagerId);

        var today = DateTime.UtcNow.Date;
        var generateFrom = command.GenerateFromDate ?? today.AddDays(1);

        var createdShifts = new List<Models.Shifts>();
        var generatedShifts = new List<GeneratedShiftDto>();
        var skipReasons = new List<SkipReason>();
        var errors = new List<string>();

        if (generateFrom <= today)
        {
            errors.Add(
                $"GenerateFromDate {generateFrom:yyyy-MM-dd} phải sau ngày hôm nay. " +
                $"Ngày tạo ca phải sau ({today:yyyy-MM-dd}).");
            logger.LogError(
                "GenerateFromDate {GenerateFromDate:yyyy-MM-dd} is not after today (today: {Today:yyyy-MM-dd})",
                generateFrom,
                today);
            var generateToError = generateFrom.AddDays(command.GenerateDays);
            return CreateErrorResult(generateFrom, generateToError, errors);
        }

        var generateTo = generateFrom.AddDays(command.GenerateDays);
        logger.LogInformation("✓ Generate date range validation passed: {From:yyyy-MM-dd} to {To:yyyy-MM-dd}", generateFrom, generateTo);

        using var connection = await connectionFactory.CreateConnectionAsync();

        try
        {
            var manager = await connection.QueryFirstOrDefaultAsync<Managers>(
                @"SELECT * FROM managers
                  WHERE Id = @ManagerId
                  AND IsDeleted = 0
                  AND IsActive = 1",
                new { command.ManagerId });

            if (manager == null)
            {
                errors.Add($"Manager {command.ManagerId} not found or inactive");
                logger.LogError("Manager {ManagerId} not found", command.ManagerId);
                return CreateErrorResult(generateFrom, generateTo, errors);
            }

            if (!manager.CanCreateShifts)
            {
                errors.Add($"Manager {manager.FullName} does not have permission to create shifts");
                logger.LogError("Manager {ManagerId} lacks CanCreateShifts permission", command.ManagerId);
                return CreateErrorResult(generateFrom, generateTo, errors);
            }

            logger.LogInformation(
                "Manager validated: {FullName} ({EmployeeCode})",
                manager.FullName,
                manager.EmployeeCode);
            
            if (!command.ShiftTemplateIds.Any())
            {
                errors.Add("ShiftTemplateIds list is empty");
                return CreateErrorResult(generateFrom, generateTo, errors);
            }

            var templates = (await connection.QueryAsync<ShiftTemplates>(
                @"SELECT * FROM shift_templates
                  WHERE Id IN @TemplateIds
                  AND IsActive = 1
                  AND IsDeleted = 0",
                new { TemplateIds = command.ShiftTemplateIds })).ToList();

            if (!templates.Any())
            {
                errors.Add($"No active shift templates found for provided IDs");
                logger.LogError("No shift templates found for IDs: {TemplateIds}",
                    string.Join(", ", command.ShiftTemplateIds));
                return CreateErrorResult(generateFrom, generateTo, errors);
            }
            
            var contractId = templates.First().ContractId ?? Guid.Empty;

            logger.LogInformation(
                "Found {TemplateCount} templates for Contract {ContractId}",
                templates.Count,
                contractId);
            
            var allDates = GenerateDateRange(generateFrom, generateTo);

            var holidayMap = await BatchCheckPublicHolidays(allDates, cancellationToken);

            logger.LogInformation(
                "Batch holiday check completed: {TotalDays} days, {HolidayCount} holidays",
                allDates.Count,
                holidayMap.Count(h => h.Value != null));
            
            var existingShifts = await PreLoadExistingShifts(
                connection,
                templates.Select(t => t.LocationId).Distinct().ToList(),
                allDates.First(),
                allDates.Last());

            logger.LogInformation(
                "Pre-loaded {Count} existing shifts for duplicate detection",
                existingShifts.Count);
            
            foreach (var template in templates)
            {
                try
                {
                    await GenerateShiftsForTemplateOptimized(
                        template,
                        allDates,
                        holidayMap,
                        existingShifts,
                        command.ManagerId,
                        createdShifts,
                        generatedShifts,
                        skipReasons,
                        errors);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Error processing template {TemplateName}",
                        template.TemplateName);
                    errors.Add($"Template {template.TemplateName}: {ex.Message}");
                }
            }

            if (createdShifts.Any())
            {
                await BulkInsertShifts(connection, createdShifts);

                logger.LogInformation(
                    "Bulk inserted {Count} shifts in single transaction",
                    createdShifts.Count);
                
                var templatesWithShifts = createdShifts
                    .Select(s => s.ShiftTemplateId)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();

                if (templatesWithShifts.Any())
                {
                    var updatedCount = await UpdateShiftTemplateStatus(
                        connection,
                        templatesWithShifts,
                        "created_shift",
                        command.ManagerId);

                    logger.LogInformation(
                        "Updated {Count} shift templates from 'await_create_shift' to 'created_shift'",
                        updatedCount);
                }

                await AutoAssignTeamsToShifts(
                    connection,
                    createdShifts,
                    templates,
                    command.ManagerId,
                    cancellationToken);
            }

            var endTime = DateTime.UtcNow;
            var durationMs = (int)(endTime - startTime).TotalMilliseconds;

            var status = errors.Any() ? "partial" :
                         createdShifts.Any() ? "success" :
                         "failed";

            var shiftsGeneratedEvent = new ShiftsGeneratedEvent
            {
                ContractId = contractId,
                ContractNumber = $"Contract-{contractId}",
                ContractShiftScheduleId = null,
                GenerationDate = generateFrom,
                GeneratedAt = endTime,
                GeneratedByJob = $"Manager_{manager.EmployeeCode}",

                ShiftsCreatedCount = createdShifts.Count,
                ShiftsSkippedCount = skipReasons.Count,
                SkipReasons = skipReasons.Select(s => s.Reason).Distinct().ToList(),
                Status = status,
                ErrorMessage = errors.Any() ? string.Join("; ", errors) : null,

                CreatedShiftIds = createdShifts.Select(s => s.Id).ToList(),
                GeneratedShifts = generatedShifts,

                LocationsProcessed = templates.Select(t => t.LocationId).Distinct().Count(),
                SchedulesProcessed = templates.Count,
                GenerationDurationMs = durationMs
            };

            await publishEndpoint.Publish(shiftsGeneratedEvent, cancellationToken);

            logger.LogInformation(
                @"Shift generation completed:
                  - Manager: {ManagerName} ({EmployeeCode})
                  - Templates Processed: {TemplateCount}
                  - Shifts Created: {CreatedCount}
                  - Shifts Skipped: {SkippedCount}
                  - Duration: {Duration}ms ({DurationSeconds}s)
                  - Performance: {ShiftsPerSecond} shifts/sec
                  - Status: {Status}",
                manager.FullName,
                manager.EmployeeCode,
                templates.Count,
                createdShifts.Count,
                skipReasons.Count,
                durationMs,
                Math.Round(durationMs / 1000.0, 2),
                Math.Round(createdShifts.Count / (durationMs / 1000.0), 2),
                status);

            return new GenerateShiftsResult
            {
                ShiftsCreatedCount = createdShifts.Count,
                ShiftsSkippedCount = skipReasons.Count,
                SkipReasons = skipReasons,
                CreatedShiftIds = createdShifts.Select(s => s.Id).ToList(),
                Errors = errors,
                GeneratedFrom = generateFrom,
                GeneratedTo = generateTo
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error during OPTIMIZED shift generation");
            errors.Add($"Fatal error: {ex.Message}");
            return CreateErrorResult(generateFrom, generateTo, errors);
        }
    }
    
    private List<DateTime> GenerateDateRange(DateTime from, DateTime to)
    {
        var dates = new List<DateTime>();
        for (var date = from; date < to; date = date.AddDays(1))
        {
            dates.Add(date);
        }
        return dates;
    }
    
    private async Task<Dictionary<DateTime, HolidayInfo?>> BatchCheckPublicHolidays(
        List<DateTime> dates,
        CancellationToken cancellationToken)
    {
        try
        {
            var batchRequest = new BatchCheckPublicHolidaysRequest
            {
                Dates = dates
            };

            var response = await batchHolidayClient.GetResponse<BatchCheckPublicHolidaysResponse>(
                batchRequest,
                cancellationToken,
                timeout: RequestTimeout.After(s: 10)); 

            return response.Message.Holidays;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to batch check public holidays for {Count} dates. Assuming no holidays.",
                dates.Count);
            return dates.ToDictionary(d => d, d => (HolidayInfo?)null);
        }
    }
    
    private async Task<HashSet<string>> PreLoadExistingShifts(
        IDbConnection connection,
        List<Guid?> locationIds,
        DateTime from,
        DateTime to)
    {
        var validLocationIds = locationIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();

        if (!validLocationIds.Any())
            return new HashSet<string>();

        var existing = await connection.QueryAsync<dynamic>(
            @"SELECT LocationId, ShiftDate, ShiftStart, ShiftEnd
              FROM shifts
              WHERE LocationId IN @LocationIds
                AND ShiftDate >= @From
                AND ShiftDate < @To
                AND IsDeleted = 0",
            new
            {
                LocationIds = validLocationIds,
                From = from,
                To = to
            });

        var keys = new HashSet<string>();
        foreach (var shift in existing)
        {
            var key = CreateShiftKey(
                (Guid)shift.LocationId,
                (DateTime)shift.ShiftDate,
                (DateTime)shift.ShiftStart,
                (DateTime)shift.ShiftEnd);
            keys.Add(key);
        }

        return keys;
    }

    private string CreateShiftKey(Guid locationId, DateTime shiftDate, DateTime shiftStart, DateTime shiftEnd)
    {
        return $"{locationId}_{shiftDate:yyyyMMdd}_{shiftStart:HHmm}_{shiftEnd:HHmm}";
    }

    private Task GenerateShiftsForTemplateOptimized(
        ShiftTemplates template,
        List<DateTime> dates,
        Dictionary<DateTime, HolidayInfo?> holidayMap,
        HashSet<string> existingShifts,
        Guid managerId,
        List<Models.Shifts> createdShifts,
        List<GeneratedShiftDto> generatedShifts,
        List<SkipReason> skipReasons,
        List<string> errors)
    {
        foreach (var date in dates)
        {
            try
            {
                var dayOfWeek = (int)date.DayOfWeek;
                if (!DayOfWeekMatches(dayOfWeek, template))
                {
                    skipReasons.Add(new SkipReason
                    {
                        Date = date,
                        LocationId = template.LocationId,
                        LocationName = template.LocationName ?? "Unknown",
                        ScheduleName = template.TemplateName,
                        Reason = $"Template does not apply on {date.DayOfWeek}"
                    });
                    continue;
                }

                if (template.EffectiveFrom.HasValue && date < template.EffectiveFrom.Value)
                {
                    skipReasons.Add(new SkipReason
                    {
                        Date = date,
                        LocationId = template.LocationId,
                        LocationName = template.LocationName ?? "Unknown",
                        ScheduleName = template.TemplateName,
                        Reason = $"Before effective date {template.EffectiveFrom.Value:yyyy-MM-dd}"
                    });
                    continue;
                }

                if (template.EffectiveTo.HasValue && date > template.EffectiveTo.Value)
                {
                    skipReasons.Add(new SkipReason
                    {
                        Date = date,
                        LocationId = template.LocationId,
                        LocationName = template.LocationName ?? "Unknown",
                        ScheduleName = template.TemplateName,
                        Reason = $"After expiration date {template.EffectiveTo.Value:yyyy-MM-dd}"
                    });
                    continue;
                }
                
                var holidayInfo = holidayMap.GetValueOrDefault(date);
                bool isPublicHoliday = holidayInfo != null;
                string holidayName = holidayInfo?.HolidayName ?? string.Empty;
                var shiftStart = date.Add(template.StartTime);
                var shiftEnd = template.CrossesMidnight
                    ? date.AddDays(1).Add(template.EndTime)
                    : date.Add(template.EndTime);

                var shiftKey = CreateShiftKey(
                    template.LocationId ?? Guid.Empty,
                    date,
                    shiftStart,
                    shiftEnd);

                if (existingShifts.Contains(shiftKey))
                {
                    skipReasons.Add(new SkipReason
                    {
                        Date = date,
                        LocationId = template.LocationId,
                        LocationName = template.LocationName ?? "Unknown",
                        ScheduleName = template.TemplateName,
                        Reason = "Shift already exists"
                    });
                    continue;
                }
                
                var shift = CreateShiftFromTemplate(
                    template,
                    date,
                    managerId,
                    isPublicHoliday,
                    holidayName,
                    dayOfWeek);

                createdShifts.Add(shift);
                
                existingShifts.Add(shiftKey);

                generatedShifts.Add(new GeneratedShiftDto
                {
                    ShiftId = shift.Id,
                    LocationId = shift.LocationId,
                    LocationName = shift.LocationName ?? "Unknown",
                    ShiftDate = date,
                    ShiftStartTime = template.StartTime,
                    ShiftEndTime = template.EndTime,
                    RequiredGuards = shift.RequiredGuards,
                    ShiftType = shift.ShiftType,
                    IsHoliday = isPublicHoliday,
                    IsWeekend = shift.IsSaturday || shift.IsSunday
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to create shift from template {TemplateName} for {Date}",
                    template.TemplateName,
                    date.ToString("yyyy-MM-dd"));

                errors.Add($"Template '{template.TemplateName}' on {date:yyyy-MM-dd}: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }
    
    private async Task BulkInsertShifts(IDbConnection connection, List<Models.Shifts> shifts)
    {
        var transaction = connection.BeginTransaction();

        try
        {
            const int batchSize = 100;
            var batches = shifts.Chunk(batchSize);

            foreach (var batch in batches)
            {
                await connection.InsertAsync(batch, transaction);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private async Task<int> UpdateShiftTemplateStatus(
        IDbConnection connection,
        List<Guid> templateIds,
        string newStatus,
        Guid managerId)
    {
        try
        {
            var sql = @"
                UPDATE shift_templates
                SET Status = @NewStatus,
                    UpdatedAt = @UpdatedAt,
                    UpdatedBy = @UpdatedBy
                WHERE Id IN @TemplateIds
                  AND Status = 'await_create_shift'
                  AND IsDeleted = 0";

            var affectedRows = await connection.ExecuteAsync(sql, new
            {
                NewStatus = newStatus,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = managerId,
                TemplateIds = templateIds
            });

            return affectedRows;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to update shift template status for {Count} templates",
                templateIds.Count);
            return 0;
        }
    }

    private bool DayOfWeekMatches(int dayOfWeek, ShiftTemplates template)
    {
        return dayOfWeek switch
        {
            0 => template.AppliesSunday,
            1 => template.AppliesMonday,
            2 => template.AppliesTuesday,
            3 => template.AppliesWednesday,
            4 => template.AppliesThursday,
            5 => template.AppliesFriday,
            6 => template.AppliesSaturday,
            _ => false
        };
    }

    private Models.Shifts CreateShiftFromTemplate(
        ShiftTemplates template,
        DateTime date,
        Guid managerId,
        bool isPublicHoliday,
        string holidayName,
        int dayOfWeek)
    {
        var shiftStart = date.Add(template.StartTime);
        var shiftEnd = template.CrossesMidnight
            ? date.AddDays(1).Add(template.EndTime)
            : date.Add(template.EndTime);

        var totalMinutes = (int)(shiftEnd - shiftStart).TotalMinutes;
        var breakMinutes = template.BreakDurationMinutes;
        var workMinutes = totalMinutes - breakMinutes;
        
        string shiftType = "REGULAR";
        if (template.DurationHours > 12)
        {
            shiftType = "OVERTIME";
        }

        bool isNightShift = template.IsNightShift;

        return new Models.Shifts
        {
            Id = Guid.NewGuid(),
            ContractId = template.ContractId ?? Guid.Empty,
            LocationId = template.LocationId ?? Guid.Empty,
            LocationName = template.LocationName,
            LocationAddress = template.LocationAddress,
            LocationLatitude = template.LocationLatitude,
            LocationLongitude = template.LocationLongitude,
            ShiftTemplateId = template.Id,
            ShiftDate = date,
            ShiftDay = date.Day,
            ShiftMonth = date.Month,
            ShiftYear = date.Year,
            ShiftQuarter = (date.Month - 1) / 3 + 1,
            ShiftWeek = GetIso8601WeekOfYear(date),
            DayOfWeek = dayOfWeek == 0 ? 7 : dayOfWeek,
            ShiftStart = shiftStart,
            ShiftEnd = shiftEnd,
            ShiftEndDate = template.CrossesMidnight ? shiftEnd.Date : date,
            TotalDurationMinutes = totalMinutes,
            WorkDurationMinutes = workMinutes,
            WorkDurationHours = Math.Round((decimal)workMinutes / 60, 2),
            BreakDurationMinutes = breakMinutes,
            PaidBreakMinutes = template.PaidBreakMinutes,
            UnpaidBreakMinutes = template.UnpaidBreakMinutes,
            RequiredGuards = template.MinGuardsRequired,
            AssignedGuardsCount = 0,
            ConfirmedGuardsCount = 0,
            CheckedInGuardsCount = 0,
            CompletedGuardsCount = 0,
            IsFullyStaffed = false,
            IsUnderstaffed = true,
            StaffingPercentage = 0,
            IsRegularWeekday = dayOfWeek >= 1 && dayOfWeek <= 5,
            IsSaturday = dayOfWeek == 6,
            IsSunday = dayOfWeek == 0,
            IsPublicHoliday = isPublicHoliday,
            IsTetHoliday = holidayName.Contains("Tết", StringComparison.OrdinalIgnoreCase),
            IsNightShift = isNightShift,
            NightHours = isNightShift ? template.DurationHours : 0,
            DayHours = isNightShift ? 0 : template.DurationHours,
            ShiftType = shiftType,
            IsMandatory = false,
            IsCritical = false,
            IsTrainingShift = false,
            RequiresArmedGuard = false,
            RequiresApproval = false,
            ApprovalStatus = "APPROVED",
            ApprovedBy = managerId,
            ApprovedAt = DateTime.UtcNow,
            Status = "SCHEDULED",
            ManagerId = managerId,
            Description = $"Auto-generated from template: {template.TemplateName}" +
                         (isPublicHoliday ? $" (Holiday: {holidayName})" : "") +
                         (shiftType == "OVERTIME" ? " (OVERTIME)" : "") +
                         (isNightShift ? " (NIGHT SHIFT)" : ""),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = managerId,
            IsDeleted = false,
            Version = 1
        };
    }


    private int GetIso8601WeekOfYear(DateTime date)
    {
        var day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
        {
            date = date.AddDays(3);
        }

        return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }
    
    private GenerateShiftsResult CreateErrorResult(DateTime from, DateTime to, List<string> errors)
    {
        return new GenerateShiftsResult
        {
            ShiftsCreatedCount = 0,
            ShiftsSkippedCount = 0,
            GeneratedFrom = from,
            GeneratedTo = to,
            Errors = errors
        };
    }
    
    private async Task AutoAssignTeamsToShifts(
        IDbConnection connection,
        List<Models.Shifts> createdShifts,
        List<ShiftTemplates> templates,
        Guid managerId,
        CancellationToken cancellationToken)
    {
        var templatesWithTeam = templates
            .Where(t => t.TeamId.HasValue)
            .ToList();

        if (!templatesWithTeam.Any())
        {
            logger.LogInformation("No templates with TeamId, skipping auto-assign");
            return;
        }

        logger.LogInformation(
            "Auto-assigning teams for {Count} templates with TeamId",
            templatesWithTeam.Count);

        foreach (var template in templatesWithTeam)
        {
            try
            {
                var shiftsFromTemplate = createdShifts
                    .Where(s => s.ShiftTemplateId == template.Id)
                    .ToList();

                if (!shiftsFromTemplate.Any())
                    continue;
                
                var shiftsByDateAndSlot = shiftsFromTemplate
                    .GroupBy(s => new
                    {
                        Date = s.ShiftDate,
                        TimeSlot = ShiftClassificationHelper.ClassifyShiftTimeSlot(s.ShiftStart)
                    })
                    .ToList();

                logger.LogInformation(
                    "Template {TemplateName}: Assigning Team {TeamId} to {ShiftCount} shifts across {DateCount} dates",
                    template.TemplateName,
                    template.TeamId,
                    shiftsFromTemplate.Count,
                    shiftsByDateAndSlot.Count);
                
                foreach (var group in shiftsByDateAndSlot)
                {
                    var dateShifts = group.ToList();
                    var firstShift = dateShifts.First();

                    var assignCommand = new AssignTeamToShiftCommand(
                        TeamId: template.TeamId!.Value,
                        StartDate: group.Key.Date,
                        EndDate: group.Key.Date, 
                        ShiftTimeSlot: group.Key.TimeSlot,
                        LocationId: firstShift.LocationId,
                        ContractId: template.ContractId,
                        AssignmentType: "REGULAR",
                        AssignmentNotes: $"Auto-assigned from template {template.TemplateName}",
                        AssignedBy: managerId
                    );

                    var result = await sender.Send(assignCommand, cancellationToken);

                    if (result.Success)
                    {
                        logger.LogInformation(
                            "Auto-assigned Team {TeamId} to {Count} shifts on {Date} ({TimeSlot})",
                            template.TeamId,
                            result.TotalGuardsAssigned,
                            group.Key.Date.ToString("yyyy-MM-dd"),
                            group.Key.TimeSlot);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Failed to auto-assign Team {TeamId} on {Date}: {Errors}",
                            template.TeamId,
                            group.Key.Date.ToString("yyyy-MM-dd"),
                            string.Join(", ", result.Errors));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error auto-assigning team {TeamId} for template {TemplateName}",
                    template.TeamId,
                    template.TemplateName);
            }
        }

        logger.LogInformation("Completed auto-assigning teams");
    }
}
