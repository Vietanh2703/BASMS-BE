using Dapper;
using Dapper.Contrib.Extensions;
using MassTransit;
using BuildingBlocks.Messaging.Events;
using Shifts.API.Data;
using Shifts.API.Models;
using Shifts.API.Helpers;

namespace Shifts.API.ShiftsHandler.AssignTeamToShift;

internal class AssignTeamToShiftHandler(
    IDbConnectionFactory dbFactory,
    IPublishEndpoint publishEndpoint,
    ILogger<AssignTeamToShiftHandler> logger)
    : ICommandHandler<AssignTeamToShiftCommand, AssignTeamToShiftResult>
{
    public async Task<AssignTeamToShiftResult> Handle(
        AssignTeamToShiftCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "🚀 Starting team assignment: Team={TeamId}, {StartDate} → {EndDate}, TimeSlot={TimeSlot}, Location={LocationId}",
                request.TeamId,
                request.StartDate.ToString("yyyy-MM-dd"),
                request.EndDate.ToString("yyyy-MM-dd"),
                request.ShiftTimeSlot,
                request.LocationId);

            using var connection = await dbFactory.CreateConnectionAsync();

            var dailySummaries = new List<DailyAssignmentSummary>();
            var warnings = new List<string>();
            var errors = new List<string>();

            // ================================================================
            // BƯỚC 1: VALIDATE TEAM
            // ================================================================
            var team = await connection.GetAsync<Teams>(request.TeamId);

            if (team == null || team.IsDeleted || !team.IsActive)
            {
                errors.Add($"Team {request.TeamId} không tồn tại hoặc không active");
                return new AssignTeamToShiftResult
                {
                    Success = false,
                    Errors = errors
                };
            }

            logger.LogInformation("✓ Team validated: {TeamCode} - {TeamName}", team.TeamCode, team.TeamName);

            // ================================================================
            // BƯỚC 2: LẤY DANH SÁCH GUARDS TRONG TEAM
            // ================================================================
            var teamMembers = await connection.QueryAsync<TeamMembers>(
                @"SELECT * FROM team_members
                  WHERE TeamId = @TeamId
                    AND IsActive = 1
                    AND IsDeleted = 0",
                new { request.TeamId });

            var teamMembersList = teamMembers.ToList();

            if (!teamMembersList.Any())
            {
                errors.Add($"Team {team.TeamCode} không có thành viên nào");
                return new AssignTeamToShiftResult
                {
                    Success = false,
                    Errors = errors
                };
            }

            var guardIds = teamMembersList.Select(m => m.GuardId).ToList();

            logger.LogInformation(
                "✓ Team has {GuardCount} active members",
                guardIds.Count);

            // Lấy thông tin guards để hiển thị tên
            var guards = await connection.QueryAsync<Guards>(
                "SELECT Id, FullName, EmployeeCode FROM guards WHERE Id IN @GuardIds",
                new { GuardIds = guardIds });

            var guardDict = guards.ToDictionary(g => g.Id, g => g);

            // ================================================================
            // BƯỚC 2.5: CHECK CROSS-CONTRACT CONFLICTS
            // ================================================================
            logger.LogInformation("🔍 Checking cross-contract conflicts...");

            var crossContractConflict = await CheckCrossContractConflict(
                connection,
                request.TeamId,
                guardIds,
                request.StartDate,
                request.EndDate,
                request.ShiftTimeSlot,
                request.ContractId);

            if (crossContractConflict.HasConflict)
            {
                logger.LogWarning(
                    "⚠️ Team {TeamId} has conflicts with {Count} other contract(s)",
                    request.TeamId,
                    crossContractConflict.ConflictingContracts.Count);

                foreach (var conflict in crossContractConflict.Conflicts)
                {
                    errors.Add(
                        $"❌ CONFLICT với Contract khác: " +
                        $"Team đã được phân công ca {ShiftClassificationHelper.GetVietnameseSlotName(conflict.ConflictTimeSlot)} " +
                        $"ngày {conflict.ConflictDate:dd/MM/yyyy} " +
                        $"tại {conflict.LocationName} " +
                        $"(Contract ID: {conflict.ContractId}). " +
                        $"Số guards bị ảnh hưởng: {conflict.AffectedGuardsCount}/{guardIds.Count}. " +
                        $"{conflict.Reason}");
                }

                logger.LogError(
                    "❌ Cannot assign team due to {Count} cross-contract conflict(s)",
                    crossContractConflict.Conflicts.Count);

                return new AssignTeamToShiftResult
                {
                    Success = false,
                    Errors = errors,
                    Warnings = warnings
                };
            }

            logger.LogInformation("✓ No cross-contract conflicts detected");

            // ================================================================
            // BƯỚC 3: XỬ LÝ TỪNG NGÀY (Multi-day loop)
            // ================================================================
            var currentDate = request.StartDate.Date;
            var totalGuardsAssigned = 0;

            while (currentDate <= request.EndDate.Date)
            {
                logger.LogInformation(
                    "📅 Processing date: {Date}",
                    currentDate.ToString("yyyy-MM-dd"));

                // ================================================================
                // BƯỚC 3.1: TÌM SHIFT TRONG NGÀY VỚI TIMESLOT PHÙ HỢP
                // ================================================================
                var shift = await FindShiftForDateAndTimeSlot(
                    connection,
                    request.LocationId,
                    currentDate,
                    request.ShiftTimeSlot,
                    request.ContractId);

                if (shift == null)
                {
                    warnings.Add(
                        $"Ngày {currentDate:yyyy-MM-dd}: Không tìm thấy ca {ShiftClassificationHelper.GetVietnameseSlotName(request.ShiftTimeSlot)}");
                    currentDate = currentDate.AddDays(1);
                    continue;
                }

                logger.LogInformation(
                    "✓ Found shift {ShiftId}: {Start} - {End}",
                    shift.Id,
                    shift.ShiftStart.ToString("yyyy-MM-dd HH:mm"),
                    shift.ShiftEnd.ToString("yyyy-MM-dd HH:mm"));

                // ================================================================
                // BƯỚC 3.2: CHECK CONSECUTIVE SHIFT CONFLICTS
                // ================================================================
                var conflictResult = await CheckConsecutiveShiftConflict(
                    connection,
                    guardIds,
                    currentDate,
                    request.ShiftTimeSlot);

                // Lọc ra guards hợp lệ (không có conflict)
                var validGuardIds = guardIds
                    .Where(gId => !conflictResult.ConflictingGuards.Any(c => c.GuardId == gId))
                    .ToList();

                var skippedGuardNames = new List<string>();

                if (conflictResult.ConflictingGuards.Any())
                {
                    foreach (var conflict in conflictResult.ConflictingGuards)
                    {
                        warnings.Add(
                            $"Ngày {currentDate:yyyy-MM-dd}: {conflict.EmployeeCode} ({conflict.GuardName}) " +
                            $"đã có ca {ShiftClassificationHelper.GetVietnameseSlotName(conflict.ConflictingTimeSlot)} " +
                            $"ngày {conflict.ConflictingDate:yyyy-MM-dd} ({conflict.ConflictingShiftTime}). " +
                            $"{conflict.Reason}");

                        skippedGuardNames.Add(conflict.GuardName);
                    }

                    logger.LogWarning(
                        "⚠️ {SkippedCount}/{TotalCount} guards skipped due to consecutive shift conflict",
                        conflictResult.ConflictingGuards.Count,
                        guardIds.Count);
                }

                if (!validGuardIds.Any())
                {
                    warnings.Add(
                        $"Ngày {currentDate:yyyy-MM-dd}: Tất cả guards đều có conflict, không thể assign");
                    currentDate = currentDate.AddDays(1);
                    continue;
                }

                // ================================================================
                // BƯỚC 3.3: CREATE SHIFT ASSIGNMENTS CHO GUARDS HỢP LỆ
                // ================================================================
                var assignmentIds = new List<Guid>();
                var guardNames = new List<string>();

                foreach (var guardId in validGuardIds)
                {
                    var assignment = new ShiftAssignments
                    {
                        Id = Guid.NewGuid(),
                        ShiftId = shift.Id,
                        TeamId = request.TeamId,
                        GuardId = guardId,
                        AssignmentType = request.AssignmentType,
                        Status = "ASSIGNED",
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = request.AssignedBy,
                        AssignmentNotes = request.AssignmentNotes,
                        AttendanceSynced = false,
                        NotificationSent = false
                    };

                    await connection.InsertAsync(assignment);
                    assignmentIds.Add(assignment.Id);

                    if (guardDict.TryGetValue(guardId, out var guard))
                    {
                        guardNames.Add(guard.FullName);
                    }

                    logger.LogInformation(
                        "✓ Assigned {EmployeeCode} ({GuardName}) to shift {ShiftId}",
                        guard?.EmployeeCode ?? "Unknown",
                        guard?.FullName ?? "Unknown",
                        shift.Id);
                }

                totalGuardsAssigned += validGuardIds.Count;

                // ================================================================
                // BƯỚC 3.4: UPDATE SHIFT STAFFING COUNTS
                // ================================================================
                shift.AssignedGuardsCount += validGuardIds.Count;
                shift.IsFullyStaffed = shift.AssignedGuardsCount >= shift.RequiredGuards;
                shift.IsUnderstaffed = shift.AssignedGuardsCount < shift.RequiredGuards;
                shift.IsOverstaffed = shift.AssignedGuardsCount > shift.RequiredGuards;
                shift.StaffingPercentage = shift.RequiredGuards > 0
                    ? (decimal)shift.AssignedGuardsCount / shift.RequiredGuards * 100
                    : 0;
                shift.UpdatedAt = DateTime.UtcNow;
                shift.UpdatedBy = request.AssignedBy;

                await connection.UpdateAsync(shift);

                logger.LogInformation(
                    "✓ Updated shift staffing: {Assigned}/{Required} guards ({Percentage}%)",
                    shift.AssignedGuardsCount,
                    shift.RequiredGuards,
                    shift.StaffingPercentage);

                // ================================================================
                // BƯỚC 3.5: PUBLISH EVENTS → TẠO ATTENDANCE RECORDS
                // ================================================================
                foreach (var assignmentId in assignmentIds)
                {
                    var guardId = validGuardIds[assignmentIds.IndexOf(assignmentId)];

                    await publishEndpoint.Publish(new ShiftAssignmentCreatedEvent
                    {
                        ShiftAssignmentId = assignmentId,
                        ShiftId = shift.Id,
                        GuardId = guardId,
                        TeamId = request.TeamId,
                        ScheduledStartTime = shift.ShiftStart,
                        ScheduledEndTime = shift.ShiftEnd,
                        LocationId = shift.LocationId,
                        LocationLatitude = shift.LocationLatitude,
                        LocationLongitude = shift.LocationLongitude
                    }, cancellationToken);
                }

                logger.LogInformation(
                    "✓ Published {Count} ShiftAssignmentCreated events",
                    assignmentIds.Count);

                // Add to summary
                dailySummaries.Add(new DailyAssignmentSummary
                {
                    Date = currentDate,
                    ShiftId = shift.Id,
                    ShiftCode = $"SH-{shift.ShiftDate:yyyyMMdd}-{shift.Id.ToString()[..8]}",
                    ShiftTimeSlot = ShiftClassificationHelper.ClassifyShiftTimeSlot(shift.ShiftStart),
                    ShiftStart = shift.ShiftStart,
                    ShiftEnd = shift.ShiftEnd,
                    GuardsAssigned = validGuardIds.Count,
                    GuardNames = guardNames,
                    SkippedGuards = skippedGuardNames
                });

                currentDate = currentDate.AddDays(1);
            }

            // ================================================================
            // BƯỚC 4: TRẢ VỀ KẾT QUẢ
            // ================================================================
            logger.LogInformation(
                "✅ Team assignment completed: {ShiftsAssigned} shifts, {GuardsAssigned} total assignments, {Warnings} warnings",
                dailySummaries.Count,
                totalGuardsAssigned,
                warnings.Count);

            return new AssignTeamToShiftResult
            {
                Success = true,
                TotalDaysProcessed = (request.EndDate.Date - request.StartDate.Date).Days + 1,
                TotalShiftsAssigned = dailySummaries.Count,
                TotalGuardsAssigned = totalGuardsAssigned,
                DailySummaries = dailySummaries,
                Warnings = warnings,
                Errors = errors
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error assigning team to shifts");
            throw;
        }
    }

    /// <summary>
    /// Tìm shift theo ngày và time slot (tính toán runtime từ ShiftStart)
    /// </summary>
    private async Task<Models.Shifts?> FindShiftForDateAndTimeSlot(
        IDbConnection connection,
        Guid locationId,
        DateTime date,
        string timeSlot,
        Guid? contractId)
    {
        logger.LogInformation(
            "Searching for shift: Location={LocationId}, Date={Date}, TimeSlot={TimeSlot}",
            locationId,
            date.ToString("yyyy-MM-dd"),
            timeSlot);

        // Lấy TẤT CẢ shifts trong ngày tại location
        var query = @"
            SELECT * FROM shifts
            WHERE LocationId = @LocationId
              AND ShiftDate = @Date
              AND IsDeleted = 0
              AND Status NOT IN ('CANCELLED', 'COMPLETED')";

        if (contractId.HasValue)
        {
            query += " AND ContractId = @ContractId";
        }

        query += " ORDER BY ShiftStart ASC";

        var shifts = await connection.QueryAsync<Models.Shifts>(
            query,
            new { LocationId = locationId, Date = date, ContractId = contractId });

        // Lọc shift theo timeSlot (classify runtime)
        var matchingShift = shifts.FirstOrDefault(s =>
            ShiftClassificationHelper.ClassifyShiftTimeSlot(s.ShiftStart) == timeSlot);

        if (matchingShift == null)
        {
            logger.LogWarning(
                "No shift found for Location={LocationId}, Date={Date}, TimeSlot={TimeSlot}",
                locationId,
                date.ToString("yyyy-MM-dd"),
                timeSlot);
        }
        else
        {
            logger.LogInformation(
                "Found shift {ShiftId}: {ShiftStart} - {ShiftEnd}",
                matchingShift.Id,
                matchingShift.ShiftStart.ToString("HH:mm"),
                matchingShift.ShiftEnd.ToString("HH:mm"));
        }

        return matchingShift;
    }

    /// <summary>
    /// Kiểm tra consecutive shift conflict
    ///
    /// RULES:
    /// - MORNING: Conflict với EVENING ngày hôm trước (vì ca đêm kết thúc 6h sáng)
    /// - AFTERNOON: Conflict với MORNING cùng ngày (ca sáng kết thúc 14h)
    /// - EVENING: Conflict với AFTERNOON cùng ngày (ca chiều kết thúc 22h)
    /// </summary>
    private async Task<ConsecutiveConflictResult> CheckConsecutiveShiftConflict(
        IDbConnection connection,
        List<Guid> guardIds,
        DateTime date,
        string requestedTimeSlot)
    {
        var conflictingGuards = new List<ConflictingGuardInfo>();

        // ================================================================
        // XÁC ĐỊNH CA CẦN CHECK CONFLICT
        // ================================================================
        var checksToPerform = new List<ConsecutiveCheck>();

        switch (requestedTimeSlot)
        {
            case "MORNING":
                // Morning conflict với Evening hôm trước
                checksToPerform.Add(new ConsecutiveCheck
                {
                    CheckDate = date.AddDays(-1),
                    ConflictSlot = "EVENING",
                    Reason = "Ca đêm hôm trước kết thúc lúc 6h sáng, không đủ nghỉ ngơi"
                });
                break;

            case "AFTERNOON":
                // Afternoon conflict với Morning cùng ngày
                checksToPerform.Add(new ConsecutiveCheck
                {
                    CheckDate = date,
                    ConflictSlot = "MORNING",
                    Reason = "Ca sáng cùng ngày kết thúc lúc 14h, liền kề ca chiều"
                });
                break;

            case "EVENING":
                // Evening conflict với Afternoon cùng ngày
                checksToPerform.Add(new ConsecutiveCheck
                {
                    CheckDate = date,
                    ConflictSlot = "AFTERNOON",
                    Reason = "Ca chiều cùng ngày kết thúc lúc 22h, liền kề ca tối"
                });
                break;
        }

        // ================================================================
        // KIỂM TRA TỪNG CHECK
        // ================================================================
        foreach (var check in checksToPerform)
        {
            // Lấy tất cả shifts trong ngày cần check
            var shiftsOnCheckDate = await connection.QueryAsync<Models.Shifts>(
                @"SELECT s.* FROM shifts s
                  INNER JOIN shift_assignments sa ON sa.ShiftId = s.Id
                  WHERE sa.GuardId IN @GuardIds
                    AND s.ShiftDate = @CheckDate
                    AND sa.IsDeleted = 0
                    AND sa.Status NOT IN ('CANCELLED', 'DECLINED')
                    AND s.IsDeleted = 0
                  GROUP BY s.Id",
                new { GuardIds = guardIds, CheckDate = check.CheckDate });

            // Lọc shifts có conflict (dựa vào ShiftStart classification)
            var conflictingShifts = shiftsOnCheckDate
                .Where(s => ShiftClassificationHelper.ClassifyShiftTimeSlot(s.ShiftStart) == check.ConflictSlot)
                .ToList();

            if (!conflictingShifts.Any())
                continue;

            // Lấy guards bị conflict
            foreach (var conflictShift in conflictingShifts)
            {
                var guardsInConflict = await connection.QueryAsync<dynamic>(
                    @"SELECT
                        g.Id AS GuardId,
                        g.FullName,
                        g.EmployeeCode
                      FROM shift_assignments sa
                      INNER JOIN guards g ON g.Id = sa.GuardId
                      WHERE sa.ShiftId = @ShiftId
                        AND sa.GuardId IN @GuardIds
                        AND sa.IsDeleted = 0
                        AND sa.Status NOT IN ('CANCELLED', 'DECLINED')",
                    new { ShiftId = conflictShift.Id, GuardIds = guardIds });

                foreach (var guard in guardsInConflict)
                {
                    conflictingGuards.Add(new ConflictingGuardInfo
                    {
                        GuardId = (Guid)guard.GuardId,
                        GuardName = (string)guard.FullName,
                        EmployeeCode = (string)guard.EmployeeCode,
                        ConflictingDate = check.CheckDate,
                        ConflictingTimeSlot = check.ConflictSlot,
                        ConflictingShiftTime = $"{conflictShift.ShiftStart:HH:mm} - {conflictShift.ShiftEnd:HH:mm}",
                        Reason = check.Reason
                    });
                }
            }
        }

        return new ConsecutiveConflictResult
        {
            HasConflict = conflictingGuards.Any(),
            ConflictingGuards = conflictingGuards.Distinct().ToList()
        };
    }

    // ================================================================
    // HELPER CLASSES
    // ================================================================
    private class ConsecutiveCheck
    {
        public DateTime CheckDate { get; set; }
        public string ConflictSlot { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    private class ConflictingGuardInfo
    {
        public Guid GuardId { get; set; }
        public string GuardName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public DateTime ConflictingDate { get; set; }
        public string ConflictingTimeSlot { get; set; } = string.Empty;
        public string ConflictingShiftTime { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;

        public override bool Equals(object? obj)
        {
            return obj is ConflictingGuardInfo info && GuardId.Equals(info.GuardId);
        }

        public override int GetHashCode()
        {
            return GuardId.GetHashCode();
        }
    }

    private class ConsecutiveConflictResult
    {
        public bool HasConflict { get; set; }
        public List<ConflictingGuardInfo> ConflictingGuards { get; set; } = new();
    }

    // ================================================================
    // CROSS-CONTRACT CONFLICT DETECTION
    // ================================================================

    /// <summary>
    /// Kiểm tra conflict của team với các contract khác
    /// Bao gồm:
    /// 1. Shifts đã tạo của contract khác
    /// 2. Shift templates có TeamId (auto-assign) của contract khác
    /// </summary>
    private async Task<CrossContractConflictResult> CheckCrossContractConflict(
        IDbConnection connection,
        Guid teamId,
        List<Guid> guardIds,
        DateTime startDate,
        DateTime endDate,
        string shiftTimeSlot,
        Guid? currentContractId)
    {
        var conflicts = new List<CrossContractConflictInfo>();

        // ================================================================
        // CASE 1: Check shifts đã tạo của contract khác
        // ================================================================
        var existingShiftConflicts = await CheckExistingShiftConflicts(
            connection,
            guardIds,
            startDate,
            endDate,
            shiftTimeSlot,
            currentContractId);

        conflicts.AddRange(existingShiftConflicts);

        // ================================================================
        // CASE 2: Check shift templates có TeamId (future conflicts)
        // ================================================================
        var templateConflicts = await CheckShiftTemplateConflicts(
            connection,
            teamId,
            startDate,
            endDate,
            shiftTimeSlot,
            currentContractId);

        conflicts.AddRange(templateConflicts);

        // ================================================================
        // Return result
        // ================================================================
        return new CrossContractConflictResult
        {
            HasConflict = conflicts.Any(),
            Conflicts = conflicts,
            ConflictingContracts = conflicts
                .Select(c => c.ContractId)
                .Distinct()
                .ToList()
        };
    }

    /// <summary>
    /// Kiểm tra shifts đã tạo của contract khác mà team members đã được assign
    /// </summary>
    private async Task<List<CrossContractConflictInfo>> CheckExistingShiftConflicts(
        IDbConnection connection,
        List<Guid> guardIds,
        DateTime startDate,
        DateTime endDate,
        string shiftTimeSlot,
        Guid? currentContractId)
    {
        var conflicts = new List<CrossContractConflictInfo>();

        logger.LogInformation(
            "Checking existing shift conflicts for {GuardCount} guards from {StartDate} to {EndDate}",
            guardIds.Count,
            startDate.ToString("yyyy-MM-dd"),
            endDate.ToString("yyyy-MM-dd"));

        // Lấy tất cả shifts của contract KHÁC mà guards đã được assign
        var sql = @"
            SELECT
                s.Id AS ShiftId,
                s.ContractId,
                s.ShiftDate,
                s.ShiftStart,
                s.ShiftEnd,
                s.LocationName,
                s.LocationAddress,
                COUNT(DISTINCT sa.GuardId) AS ConflictingGuardsCount,
                GROUP_CONCAT(DISTINCT g.FullName) AS ConflictingGuardNames
            FROM shifts s
            INNER JOIN shift_assignments sa ON sa.ShiftId = s.Id
            INNER JOIN guards g ON g.Id = sa.GuardId
            WHERE sa.GuardId IN @GuardIds
              AND s.ShiftDate BETWEEN @StartDate AND @EndDate
              AND s.Status NOT IN ('CANCELLED', 'COMPLETED')
              AND sa.Status NOT IN ('CANCELLED', 'DECLINED')
              AND sa.IsDeleted = 0
              AND s.IsDeleted = 0";

        if (currentContractId.HasValue)
        {
            sql += " AND s.ContractId != @CurrentContractId AND s.ContractId IS NOT NULL";
        }
        else
        {
            sql += " AND s.ContractId IS NOT NULL";
        }

        sql += @"
            GROUP BY s.Id, s.ContractId, s.ShiftDate, s.ShiftStart, s.ShiftEnd, s.LocationName
            HAVING ConflictingGuardsCount > 0
            ORDER BY s.ShiftDate, s.ShiftStart";

        var shiftsData = await connection.QueryAsync<dynamic>(
            sql,
            new
            {
                GuardIds = guardIds,
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                CurrentContractId = currentContractId
            });

        var shiftsList = shiftsData.ToList();

        logger.LogInformation(
            "Found {Count} shifts from other contracts",
            shiftsList.Count);

        // Filter theo time slot
        foreach (var shift in shiftsList)
        {
            DateTime shiftStart = shift.ShiftStart;
            string detectedTimeSlot = ShiftClassificationHelper.ClassifyShiftTimeSlot(shiftStart);

            // Chỉ báo conflict nếu cùng time slot
            if (detectedTimeSlot == shiftTimeSlot)
            {
                conflicts.Add(new CrossContractConflictInfo
                {
                    ContractId = (Guid)shift.ContractId,
                    ConflictType = "EXISTING_SHIFT",
                    ConflictDate = ((DateTime)shift.ShiftDate).Date,
                    ConflictTimeSlot = detectedTimeSlot,
                    LocationName = shift.LocationName ?? "Unknown",
                    AffectedGuardsCount = (int)shift.ConflictingGuardsCount,
                    AffectedGuardNames = ((string)shift.ConflictingGuardNames)
                        .Split(',')
                        .Select(n => n.Trim())
                        .ToList(),
                    Reason = $"Team members đã được phân công vào ca này ({shiftStart:HH:mm}-{((DateTime)shift.ShiftEnd):HH:mm})",
                    ShiftId = (Guid)shift.ShiftId
                });
            }
        }

        logger.LogInformation(
            "Detected {Count} existing shift conflicts (same time slot)",
            conflicts.Count);

        return conflicts;
    }

    /// <summary>
    /// Kiểm tra shift templates có TeamId của contract khác (future conflicts)
    /// </summary>
    private async Task<List<CrossContractConflictInfo>> CheckShiftTemplateConflicts(
        IDbConnection connection,
        Guid teamId,
        DateTime startDate,
        DateTime endDate,
        string shiftTimeSlot,
        Guid? currentContractId)
    {
        var conflicts = new List<CrossContractConflictInfo>();

        logger.LogInformation(
            "Checking shift template conflicts for team {TeamId}",
            teamId);

        // Lấy shift templates có TeamId = team này (auto-assign)
        // Của contract KHÁC
        // Effective range overlap với assignment range
        var sql = @"
            SELECT
                Id AS TemplateId,
                ContractId,
                TemplateName,
                TemplateCode,
                StartTime,
                EndTime,
                EffectiveFrom,
                EffectiveTo,
                LocationName,
                AppliesMonday,
                AppliesTuesday,
                AppliesWednesday,
                AppliesThursday,
                AppliesFriday,
                AppliesSaturday,
                AppliesSunday
            FROM shift_templates
            WHERE TeamId = @TeamId
              AND IsActive = 1
              AND IsDeleted = 0";

        if (currentContractId.HasValue)
        {
            sql += " AND ContractId != @CurrentContractId AND ContractId IS NOT NULL";
        }
        else
        {
            sql += " AND ContractId IS NOT NULL";
        }

        sql += @"
              AND (EffectiveFrom IS NULL OR EffectiveFrom <= @EndDate)
              AND (EffectiveTo IS NULL OR EffectiveTo >= @StartDate)";

        var templates = await connection.QueryAsync<dynamic>(
            sql,
            new
            {
                TeamId = teamId,
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                CurrentContractId = currentContractId
            });

        var templatesList = templates.ToList();

        logger.LogInformation(
            "Found {Count} shift templates with TeamId from other contracts",
            templatesList.Count);

        // Kiểm tra từng template
        foreach (var template in templatesList)
        {
            TimeSpan startTime = template.StartTime;
            DateTime dummyDateTime = DateTime.Today.Add(startTime);
            string detectedTimeSlot = ShiftClassificationHelper.ClassifyShiftTimeSlot(dummyDateTime);

            // Chỉ check nếu cùng time slot
            if (detectedTimeSlot != shiftTimeSlot)
                continue;

            // Kiểm tra chi tiết từng ngày trong range
            var currentDate = startDate.Date;
            var templateEffectiveFrom = template.EffectiveFrom != null
                ? ((DateTime)template.EffectiveFrom).Date
                : DateTime.MinValue;
            var templateEffectiveTo = template.EffectiveTo != null
                ? ((DateTime)template.EffectiveTo).Date
                : DateTime.MaxValue;

            while (currentDate <= endDate.Date)
            {
                // Check ngày này có nằm trong effective range không
                if (currentDate < templateEffectiveFrom || currentDate > templateEffectiveTo)
                {
                    currentDate = currentDate.AddDays(1);
                    continue;
                }

                // Check template có apply cho ngày này không (day of week)
                var dayOfWeek = currentDate.DayOfWeek;
                bool appliesOnThisDay = dayOfWeek switch
                {
                    DayOfWeek.Monday => template.AppliesMonday,
                    DayOfWeek.Tuesday => template.AppliesTuesday,
                    DayOfWeek.Wednesday => template.AppliesWednesday,
                    DayOfWeek.Thursday => template.AppliesThursday,
                    DayOfWeek.Friday => template.AppliesFriday,
                    DayOfWeek.Saturday => template.AppliesSaturday,
                    DayOfWeek.Sunday => template.AppliesSunday,
                    _ => false
                };

                if (appliesOnThisDay)
                {
                    // CONFLICT tại ngày này
                    conflicts.Add(new CrossContractConflictInfo
                    {
                        ContractId = (Guid)template.ContractId,
                        ConflictType = "TEMPLATE_AUTO_ASSIGN",
                        ConflictDate = currentDate,
                        ConflictTimeSlot = detectedTimeSlot,
                        LocationName = template.LocationName ?? "Unknown",
                        AffectedGuardsCount = 0, // Chưa generate shift nên chưa biết số guards
                        AffectedGuardNames = new List<string>(),
                        Reason = $"Template '{template.TemplateName}' có TeamId này, sẽ auto-assign khi generate shifts ({startTime:hh\\:mm}-{((TimeSpan)template.EndTime):hh\\:mm})",
                        TemplateName = template.TemplateName,
                        TemplateId = (Guid)template.TemplateId
                    });
                }

                currentDate = currentDate.AddDays(1);
            }
        }

        logger.LogInformation(
            "Detected {Count} template conflicts (future auto-assign)",
            conflicts.Count);

        return conflicts;
    }

    // ================================================================
    // CROSS-CONTRACT CONFLICT HELPER CLASSES
    // ================================================================

    private class CrossContractConflictInfo
    {
        public Guid ContractId { get; set; }
        public string ConflictType { get; set; } = string.Empty; // EXISTING_SHIFT | TEMPLATE_AUTO_ASSIGN
        public DateTime ConflictDate { get; set; }
        public string ConflictTimeSlot { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public int AffectedGuardsCount { get; set; }
        public List<string> AffectedGuardNames { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public Guid? ShiftId { get; set; }
        public Guid? TemplateId { get; set; }
    }

    private class CrossContractConflictResult
    {
        public bool HasConflict { get; set; }
        public List<CrossContractConflictInfo> Conflicts { get; set; } = new();
        public List<Guid> ConflictingContracts { get; set; } = new();
    }
}
