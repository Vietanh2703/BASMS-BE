namespace Shifts.API.ShiftsHandler.BulkCancelShift;

public record BulkCancelShiftCommand(
    Guid GuardId,
    DateTime FromDate,
    DateTime ToDate,
    string CancellationReason,
    string LeaveType,
    Stream? EvidenceFileStream,
    string? EvidenceFileName,
    string? EvidenceContentType,
    Guid CancelledBy
) : ICommand<BulkCancelShiftResult>;


public record BulkCancelShiftResult(
    bool Success,
    string Message,
    int TotalShiftsProcessed,
    int ShiftsCancelled,
    int AssignmentsCancelled,
    int GuardsAffected,
    string? EvidenceFileUrl,
    List<ShiftCancellationDetail> Details,
    List<string> Warnings,
    List<string> Errors
);

public record ShiftCancellationDetail(
    Guid ShiftId,
    DateTime ShiftDate,
    string ShiftTimeSlot,
    TimeSpan ShiftStartTime,
    TimeSpan ShiftEndTime,
    int AssignmentsCancelled,
    bool Success,
    string? ErrorMessage
);


internal class BulkCancelShiftHandler(
    IDbConnectionFactory dbFactory,
    ISender sender,
    IPublishEndpoint publishEndpoint,
    IS3Service s3Service,
    ILogger<BulkCancelShiftHandler> logger)
    : ICommandHandler<BulkCancelShiftCommand, BulkCancelShiftResult>
{
    public async Task<BulkCancelShiftResult> Handle(
        BulkCancelShiftCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Starting bulk cancel for Guard {GuardId} from {FromDate:yyyy-MM-dd} to {ToDate:yyyy-MM-dd}",
                request.GuardId,
                request.FromDate,
                request.ToDate);

            string? evidenceFileUrl = null;

            if (request.EvidenceFileStream != null && !string.IsNullOrEmpty(request.EvidenceFileName))
            {
                logger.LogInformation(
                    "Uploading evidence file: {FileName}",
                    request.EvidenceFileName);

                var (uploadSuccess, fileUrl, uploadErrorMessage) = await s3Service.UploadFileAsync(
                    request.EvidenceFileStream,
                    request.EvidenceFileName,
                    request.EvidenceContentType ?? "application/octet-stream",
                    cancellationToken);

                if (!uploadSuccess)
                {
                    logger.LogError("Failed to upload evidence file: {ErrorMessage}", uploadErrorMessage);
                    throw new InvalidOperationException($"Upload file thất bại: {uploadErrorMessage}");
                }

                evidenceFileUrl = fileUrl;
                logger.LogInformation("Evidence file uploaded successfully: {FileUrl}", fileUrl);
            }

            using var connection = await dbFactory.CreateConnectionAsync();
            
            var guard = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT Id, EmployeeCode, FullName, Email, PhoneNumber
                FROM guards
                WHERE Id = @GuardId AND IsDeleted = 0",
                new { request.GuardId });

            if (guard == null)
            {
                throw new InvalidOperationException($"Guard {request.GuardId} không tồn tại");
            }

            logger.LogInformation(
                "✓ Found guard: {EmployeeCode} - {FullName}",
                (string)guard.EmployeeCode,
                (string)guard.FullName);

            var assignmentsQuery = @"
                SELECT sa.*, g.Email, g.FullName, g.PhoneNumber,
                       s.ShiftDate, s.ShiftStart, s.ShiftEnd, s.LocationName
                FROM shift_assignments sa
                INNER JOIN guards g ON sa.GuardId = g.Id
                INNER JOIN shifts s ON sa.ShiftId = s.Id
                WHERE sa.GuardId = @GuardId
                  AND s.ShiftDate >= @FromDate
                  AND s.ShiftDate <= @ToDate
                  AND sa.IsDeleted = 0
                  AND s.IsDeleted = 0
                  AND sa.Status NOT IN ('CANCELLED', 'COMPLETED')
                  AND s.Status NOT IN ('CANCELLED', 'COMPLETED')
                ORDER BY s.ShiftDate, s.ShiftStart";

            var assignments = await connection.QueryAsync<AssignmentWithGuardInfo>(
                assignmentsQuery,
                new
                {
                    GuardId = request.GuardId,
                    FromDate = request.FromDate.Date,
                    ToDate = request.ToDate.Date
                });

            var assignmentsList = assignments.ToList();

            if (!assignmentsList.Any())
            {
                logger.LogWarning("No assignments found for Guard {GuardId} in date range", request.GuardId);
                return new BulkCancelShiftResult(
                    Success: true,
                    Message: "Không tìm thấy ca trực nào cần hủy trong khoảng thời gian này",
                    TotalShiftsProcessed: 0,
                    ShiftsCancelled: 0,
                    AssignmentsCancelled: 0,
                    GuardsAffected: 0,
                    EvidenceFileUrl: evidenceFileUrl,
                    Details: new List<ShiftCancellationDetail>(),
                    Warnings: new List<string>(),
                    Errors: new List<string>()
                );
            }

            logger.LogInformation(
                "✓ Found {Count} assignments to cancel for guard {GuardId}",
                assignmentsList.Count,
                request.GuardId);
            
            foreach (var a in assignmentsList.Take(3))
            {
                logger.LogInformation(
                    "  - Assignment {Id}: GuardId={GuardId}, GuardName={Name}, ShiftId={ShiftId}, Date={Date}",
                    a.Id,
                    a.GuardId,
                    a.FullName,
                    a.ShiftId,
                    a.ShiftDate.ToString("yyyy-MM-dd"));
            }
            
            var affectedShiftIds = assignmentsList.Select(a => a.ShiftId).Distinct().ToList();
            
            using var transaction = connection.BeginTransaction();

            try
            {
                int assignmentsCancelled = 0;
                var details = new List<ShiftCancellationDetail>();
                var warnings = new List<string>();
                var errors = new List<string>();

                var assignmentIds = assignmentsList.Select(a => a.Id).ToList();

                logger.LogInformation(
                    "About to cancel {Count} assignment IDs: [{Ids}] for GuardId: {GuardId}",
                    assignmentIds.Count,
                    string.Join(", ", assignmentIds.Take(5)),
                    request.GuardId);

                var updateAssignmentsSql = @"
                    UPDATE shift_assignments
                    SET
                        Status = 'CANCELLED',
                        CancelledAt = @CancelledAt,
                        CancellationReason = @CancellationReason,
                        UpdatedAt = @UpdatedAt
                    WHERE Id IN @AssignmentIds
                      AND GuardId = @GuardId
                      AND IsDeleted = 0
                      AND Status NOT IN ('CANCELLED', 'COMPLETED')";

                assignmentsCancelled = await connection.ExecuteAsync(
                    updateAssignmentsSql,
                    new
                    {
                        AssignmentIds = assignmentIds,
                        GuardId = request.GuardId, 
                        CancelledAt = DateTime.UtcNow,
                        CancellationReason = request.CancellationReason,
                        UpdatedAt = DateTime.UtcNow
                    },
                    transaction);

                logger.LogInformation(
                    "✓ Cancelled {Count} assignments for guard {GuardId}",
                    assignmentsCancelled,
                    request.GuardId);
                
                var verifyQuery = @"
                    SELECT COUNT(DISTINCT GuardId) as GuardCount
                    FROM shift_assignments
                    WHERE Id IN @AssignmentIds
                      AND Status = 'CANCELLED'
                      AND CancellationReason = @CancellationReason";

                var verifyResult = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    verifyQuery,
                    new { AssignmentIds = assignmentIds, CancellationReason = request.CancellationReason },
                    transaction);

                int distinctGuardsAffected = verifyResult?.GuardCount != null
                    ? Convert.ToInt32((long)verifyResult.GuardCount)
                    : 0;

                if (distinctGuardsAffected > 1)
                {
                    logger.LogError(
                        "WARNING: {Count} distinct guards were affected! Expected only 1 guard ({GuardId})",
                        distinctGuardsAffected,
                        request.GuardId);
                    
                    transaction.Rollback();
                    throw new InvalidOperationException(
                        $"Logic error: {distinctGuardsAffected} guards were affected instead of 1. Transaction rolled back.");
                }

                logger.LogInformation("Verified: Only 1 guard affected (GuardId: {GuardId})", request.GuardId);

                foreach (var shiftId in affectedShiftIds)
                {
                    var countsSql = @"
                        SELECT
                            COUNT(*) as TotalAssignments,
                            SUM(CASE WHEN Status = 'CONFIRMED' THEN 1 ELSE 0 END) as ConfirmedCount,
                            SUM(CASE WHEN Status = 'CHECKED_IN' OR Status = 'CHECKED_OUT' THEN 1 ELSE 0 END) as CheckedInCount,
                            SUM(CASE WHEN Status = 'COMPLETED' THEN 1 ELSE 0 END) as CompletedCount
                        FROM shift_assignments
                        WHERE ShiftId = @ShiftId
                          AND IsDeleted = 0
                          AND Status NOT IN ('CANCELLED')";

                    var counts = await connection.QueryFirstOrDefaultAsync<dynamic>(
                        countsSql,
                        new { ShiftId = shiftId },
                        transaction);
                    
                    var requiredGuards = await connection.QueryFirstOrDefaultAsync<int>(
                        "SELECT RequiredGuards FROM shifts WHERE Id = @ShiftId",
                        new { ShiftId = shiftId },
                        transaction);
                    
                    int totalAssignments = counts?.TotalAssignments != null ? Convert.ToInt32((long)counts.TotalAssignments) : 0;
                    int confirmedCount = counts?.ConfirmedCount != null ? Convert.ToInt32((long)counts.ConfirmedCount) : 0;
                    int checkedInCount = counts?.CheckedInCount != null ? Convert.ToInt32((long)counts.CheckedInCount) : 0;
                    int completedCount = counts?.CompletedCount != null ? Convert.ToInt32((long)counts.CompletedCount) : 0;
                    bool isFullyStaffed = totalAssignments >= requiredGuards;
                    bool isUnderstaffed = totalAssignments < requiredGuards;
                    decimal staffingPercentage = requiredGuards > 0
                        ? (decimal)totalAssignments / requiredGuards * 100
                        : 0;
                    
                    var updateShiftCountersSql = @"
                        UPDATE shifts
                        SET
                            AssignedGuardsCount = @AssignedCount,
                            ConfirmedGuardsCount = @ConfirmedCount,
                            CheckedInGuardsCount = @CheckedInCount,
                            CompletedGuardsCount = @CompletedCount,
                            IsFullyStaffed = @IsFullyStaffed,
                            IsUnderstaffed = @IsUnderstaffed,
                            StaffingPercentage = @StaffingPercentage,
                            UpdatedAt = @UpdatedAt,
                            UpdatedBy = @UpdatedBy,
                            Version = Version + 1
                        WHERE Id = @ShiftId";

                    await connection.ExecuteAsync(
                        updateShiftCountersSql,
                        new
                        {
                            ShiftId = shiftId,
                            AssignedCount = totalAssignments,
                            ConfirmedCount = confirmedCount,
                            CheckedInCount = checkedInCount,
                            CompletedCount = completedCount,
                            IsFullyStaffed = isFullyStaffed,
                            IsUnderstaffed = isUnderstaffed,
                            StaffingPercentage = staffingPercentage,
                            UpdatedAt = DateTime.UtcNow,
                            UpdatedBy = request.CancelledBy
                        },
                        transaction);
                }

                logger.LogInformation(
                    "Updated counters for {Count} affected shifts",
                    affectedShiftIds.Count);
                
                foreach (var assignment in assignmentsList)
                {
                    details.Add(new ShiftCancellationDetail(
                        ShiftId: assignment.ShiftId,
                        ShiftDate: assignment.ShiftDate,
                        ShiftTimeSlot: ShiftClassificationHelper.ClassifyShiftTimeSlot(assignment.ShiftStart),
                        ShiftStartTime: assignment.ShiftStart.TimeOfDay,
                        ShiftEndTime: assignment.ShiftEnd.TimeOfDay,
                        AssignmentsCancelled: 1,
                        Success: true,
                        ErrorMessage: null
                    ));
                }


                logger.LogInformation("Publishing {Count} ShiftAssignmentCancelledEvent...", assignmentsList.Count);

                foreach (var assignment in assignmentsList)
                {
                    await publishEndpoint.Publish(new ShiftAssignmentCancelledEvent
                    {
                        ShiftAssignmentId = assignment.Id,
                        ShiftId = assignment.ShiftId,
                        GuardId = assignment.GuardId,
                        CancellationReason = request.CancellationReason,
                        LeaveType = request.LeaveType,
                        CancelledAt = DateTime.UtcNow,
                        CancelledBy = request.CancelledBy,
                        EvidenceImageUrl = evidenceFileUrl
                    }, cancellationToken);
                }

                logger.LogInformation(
                    "✓ Published {Count} events to sync with Attendances.API",
                    assignmentsList.Count);

                transaction.Commit();

                logger.LogInformation(
                    "Bulk cancel committed: {Shifts} shifts affected, {Assignments} assignments cancelled",
                    affectedShiftIds.Count,
                    assignmentsCancelled);
                
                var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));

                var issueRecord = new
                {
                    Id = Guid.NewGuid(),
                    ShiftId = (Guid?)null,
                    GuardId = request.GuardId,
                    IssueType = request.LeaveType switch
                    {
                        "SICK_LEAVE" => "SICK_LEAVE",
                        "MATERNITY_LEAVE" => "MATERNITY_LEAVE",
                        "LONG_TERM_LEAVE" => "LONG_TERM_LEAVE",
                        _ => "BULK_CANCEL"
                    },
                    Reason = request.CancellationReason,
                    StartDate = request.FromDate.Date,
                    EndDate = request.ToDate.Date,
                    IssueDate = vietnamNow,
                    EvidenceFileUrl = evidenceFileUrl,
                    TotalShiftsAffected = affectedShiftIds.Count, 
                    TotalGuardsAffected = 1,
                    CreatedAt = vietnamNow,
                    CreatedBy = request.CancelledBy,
                    UpdatedAt = (DateTime?)null,
                    UpdatedBy = (Guid?)null,
                    IsDeleted = false,
                    DeletedAt = (DateTime?)null,
                    DeletedBy = (Guid?)null
                };

                await connection.ExecuteAsync(@"
                    INSERT INTO shift_issues (
                        Id, ShiftId, GuardId, IssueType, Reason,
                        StartDate, EndDate, IssueDate, EvidenceFileUrl,
                        TotalShiftsAffected, TotalGuardsAffected,
                        CreatedAt, CreatedBy, UpdatedAt, UpdatedBy,
                        IsDeleted, DeletedAt, DeletedBy
                    ) VALUES (
                        @Id, @ShiftId, @GuardId, @IssueType, @Reason,
                        @StartDate, @EndDate, @IssueDate, @EvidenceFileUrl,
                        @TotalShiftsAffected, @TotalGuardsAffected,
                        @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy,
                        @IsDeleted, @DeletedAt, @DeletedBy
                    )", issueRecord);

                logger.LogInformation(
                    "✓ Saved bulk shift issue record: {IssueId}, Type: {IssueType}, Assignments: {Assignments}, Guard: {GuardName}",
                    issueRecord.Id,
                    issueRecord.IssueType,
                    assignmentsCancelled,
                    (string)guard.FullName);

                _ = Task.Run(async () =>
                {
                    await SendBulkCancellationNotifications(
                        guard,
                        assignmentsList,
                        request.CancellationReason,
                        request.LeaveType,
                        evidenceFileUrl,
                        cancellationToken);
                }, cancellationToken);
                
                return new BulkCancelShiftResult(
                    Success: true,
                    Message: $"Đã hủy thành công {assignmentsCancelled} assignment(s) trong {affectedShiftIds.Count} ca trực cho bảo vệ {guard.FullName}",
                    TotalShiftsProcessed: affectedShiftIds.Count,
                    ShiftsCancelled: 0, 
                    AssignmentsCancelled: assignmentsCancelled,
                    GuardsAffected: 1, 
                    EvidenceFileUrl: evidenceFileUrl,
                    Details: details,
                    Warnings: warnings,
                    Errors: errors
                );
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                logger.LogError(ex, "Bulk cancel failed, transaction rolled back");
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in bulk cancel shift");
            throw;
        }
    }
    
    private async Task SendBulkCancellationNotifications(
        dynamic guard,
        List<AssignmentWithGuardInfo> assignments,
        string cancellationReason,
        string leaveType,
        string? evidenceImageUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Sending bulk cancellation notifications...");
            if (!string.IsNullOrEmpty(guard.Email))
            {
                var leaveTypeName = leaveType switch
                {
                    "SICK_LEAVE" => "Nghỉ ốm",
                    "MATERNITY_LEAVE" => "Nghỉ thai sản",
                    "LONG_TERM_LEAVE" => "Nghỉ phép dài hạn",
                    _ => "Nghỉ việc"
                };
                var shiftList = string.Join("\n", assignments.Select(a =>
                    $"- Ngày {a.ShiftDate:dd/MM/yyyy}: {ShiftClassificationHelper.ClassifyShiftTimeSlot(a.ShiftStart)} ({a.ShiftStart.TimeOfDay:hh\\:mm}-{a.ShiftEnd.TimeOfDay:hh\\:mm}) tại {a.LocationName ?? "N/A"}"));

                var emailBody = $@"
{leaveTypeName} từ {assignments.Min(a => a.ShiftDate):dd/MM/yyyy} đến {assignments.Max(a => a.ShiftDate):dd/MM/yyyy}|
Lý do: {cancellationReason}|
Số ca bị hủy: {assignments.Count}|
{shiftList}|
{evidenceImageUrl ?? ""}";

                await sender.Send(new SendEmailNotificationCommand(
                    GuardName: guard.FullName,
                    GuardEmail: guard.Email,
                    ShiftDate: assignments.Min(a => a.ShiftDate),
                    StartTime: TimeSpan.Zero,
                    EndTime: TimeSpan.Zero,
                    Location: "",
                    EmailType: "BULK_CANCELLATION",
                    AdditionalInfo: emailBody
                ), cancellationToken);

                logger.LogInformation(
                    "✓ Sent bulk cancellation email to guard {GuardName} ({Email})",
                    (string)guard.FullName,
                    (string)guard.Email);
            }
            
            var leaveTypeDisplay = leaveType switch
            {
                "SICK_LEAVE" => "nghỉ ốm dài ngày",
                "MATERNITY_LEAVE" => "nghỉ thai sản",
                "LONG_TERM_LEAVE" => "nghỉ phép dài hạn",
                _ => "nghỉ việc"
            };

            var affectedShiftsCount = assignments.Select(a => a.ShiftId).Distinct().Count();

            var directorEmailBody = $@"
Báo cáo: Bảo vệ {guard.FullName} (#{guard.EmployeeCode}) {leaveTypeDisplay}|
Thời gian nghỉ: {assignments.Min(a => a.ShiftDate):dd/MM/yyyy} - {assignments.Max(a => a.ShiftDate):dd/MM/yyyy}|
Lý do: {cancellationReason}|
Số assignment bị hủy: {assignments.Count} assignment(s) trong {affectedShiftsCount} ca|
{evidenceImageUrl ?? ""}";

            await sender.Send(new SendEmailNotificationCommand(
                GuardName: "Director",
                GuardEmail: "director@basms.com",
                ShiftDate: assignments.Min(a => a.ShiftDate),
                StartTime: TimeSpan.Zero,
                EndTime: TimeSpan.Zero,
                Location: "",
                EmailType: "DIRECTOR_BULK_CANCELLATION",
                AdditionalInfo: directorEmailBody
            ), cancellationToken);

            logger.LogInformation("Sent bulk cancellation report to director@basms.com");
            
            await sender.Send(new SendNotificationCommand(
                ShiftId: assignments.First().ShiftId,
                ContractId: null,
                RecipientId: (Guid)guard.Id,
                RecipientType: "GUARD",
                Action: "BULK_SHIFT_CANCELLED",
                Title: $"{assignments.Count} ca trực đã bị hủy",
                Message: $"Các ca trực từ {assignments.Min(a => a.ShiftDate):dd/MM/yyyy} đến {assignments.Max(a => a.ShiftDate):dd/MM/yyyy} đã bị hủy. Lý do: {cancellationReason}",
                Metadata: null,
                Priority: "HIGH"
            ), cancellationToken);

            logger.LogInformation("Sent in-app notification to guard");

            logger.LogInformation("All notifications sent successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending bulk cancellation notifications");
        }
    }
}

internal class AssignmentWithGuardInfo
{
    public Guid Id { get; set; }
    public Guid ShiftId { get; set; }
    public Guid GuardId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime ShiftDate { get; set; }
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public string? LocationName { get; set; }
}
