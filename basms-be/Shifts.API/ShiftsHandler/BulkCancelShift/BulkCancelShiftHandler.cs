using Dapper;
using MassTransit;
using BuildingBlocks.Messaging.Events;
using Shifts.API.Handlers.SendNotification;
using Shifts.API.Handlers.SendEmailNotification;
using Shifts.API.Helpers;
using Shifts.API.Extensions;

namespace Shifts.API.ShiftsHandler.BulkCancelShift;

// ============================================================================
// COMMAND & RESULT
// ============================================================================

/// <summary>
/// Command để hủy nhiều ca trực cùng lúc (ốm dài ngày, thai sản)
/// </summary>
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

/// <summary>
/// Kết quả bulk cancel
/// </summary>
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

/// <summary>
/// Chi tiết từng shift bị cancel
/// </summary>
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

// ============================================================================
// HANDLER
// ============================================================================

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
                "🔄 Starting bulk cancel for Guard {GuardId} from {FromDate:yyyy-MM-dd} to {ToDate:yyyy-MM-dd}",
                request.GuardId,
                request.FromDate,
                request.ToDate);

            // ================================================================
            // BƯỚC 0: UPLOAD FILE LÊN AWS S3 (NẾU CÓ)
            // ================================================================
            string? evidenceFileUrl = null;

            if (request.EvidenceFileStream != null && !string.IsNullOrEmpty(request.EvidenceFileName))
            {
                logger.LogInformation(
                    "📁 Uploading evidence file: {FileName}",
                    request.EvidenceFileName);

                var (uploadSuccess, fileUrl, uploadErrorMessage) = await s3Service.UploadFileAsync(
                    request.EvidenceFileStream,
                    request.EvidenceFileName,
                    request.EvidenceContentType ?? "application/octet-stream",
                    cancellationToken);

                if (!uploadSuccess)
                {
                    logger.LogError("❌ Failed to upload evidence file: {ErrorMessage}", uploadErrorMessage);
                    throw new InvalidOperationException($"Upload file thất bại: {uploadErrorMessage}");
                }

                evidenceFileUrl = fileUrl;
                logger.LogInformation("✅ Evidence file uploaded successfully: {FileUrl}", fileUrl);
            }

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: LẤY THÔNG TIN GUARD
            // ================================================================
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

            // ================================================================
            // BƯỚC 2: TÌM TẤT CẢ ASSIGNMENTS CỦA GUARD NÀY TRONG DATE RANGE
            // CHỈ CANCEL ASSIGNMENTS CỦA GUARD NÀY, KHÔNG CANCEL SHIFT!
            // ================================================================
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
                logger.LogWarning("⚠️ No assignments found for Guard {GuardId} in date range", request.GuardId);
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

            // Lấy danh sách ShiftIds để cập nhật counters sau
            var affectedShiftIds = assignmentsList.Select(a => a.ShiftId).Distinct().ToList();

            // ================================================================
            // BƯỚC 3: BEGIN TRANSACTION - BULK UPDATE DATABASE
            // ================================================================
            using var transaction = connection.BeginTransaction();

            try
            {
                int assignmentsCancelled = 0;
                var details = new List<ShiftCancellationDetail>();
                var warnings = new List<string>();
                var errors = new List<string>();

                // ============================================================
                // 3.1. CANCEL CHỈ ASSIGNMENTS CỦA GUARD NÀY
                // ============================================================
                var assignmentIds = assignmentsList.Select(a => a.Id).ToList();

                var updateAssignmentsSql = @"
                    UPDATE shift_assignments
                    SET
                        Status = 'CANCELLED',
                        CancelledAt = @CancelledAt,
                        CancellationReason = @CancellationReason,
                        UpdatedAt = @UpdatedAt
                    WHERE Id IN @AssignmentIds
                      AND IsDeleted = 0
                      AND Status NOT IN ('CANCELLED', 'COMPLETED')";

                assignmentsCancelled = await connection.ExecuteAsync(
                    updateAssignmentsSql,
                    new
                    {
                        AssignmentIds = assignmentIds,
                        CancelledAt = DateTime.UtcNow,
                        CancellationReason = request.CancellationReason,
                        UpdatedAt = DateTime.UtcNow
                    },
                    transaction);

                logger.LogInformation(
                    "✓ Cancelled {Count} assignments for guard {GuardId}",
                    assignmentsCancelled,
                    request.GuardId);

                // ============================================================
                // 3.2. CẬP NHẬT COUNTERS CỦA TỪNG SHIFT BỊ ẢNH HƯỞNG
                // ============================================================
                foreach (var shiftId in affectedShiftIds)
                {
                    // Đếm lại số guards còn lại sau khi cancel
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

                    // Lấy RequiredGuards để tính staffing status
                    var requiredGuards = await connection.QueryFirstOrDefaultAsync<int>(
                        "SELECT RequiredGuards FROM shifts WHERE Id = @ShiftId",
                        new { ShiftId = shiftId },
                        transaction);

                    int totalAssignments = counts?.TotalAssignments ?? 0;
                    int confirmedCount = counts?.ConfirmedCount ?? 0;
                    int checkedInCount = counts?.CheckedInCount ?? 0;
                    int completedCount = counts?.CompletedCount ?? 0;

                    // Tính staffing status
                    bool isFullyStaffed = totalAssignments >= requiredGuards;
                    bool isUnderstaffed = totalAssignments < requiredGuards;
                    decimal staffingPercentage = requiredGuards > 0
                        ? (decimal)totalAssignments / requiredGuards * 100
                        : 0;

                    // Cập nhật shift counters
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
                    "✓ Updated counters for {Count} affected shifts",
                    affectedShiftIds.Count);

                // ============================================================
                // 3.3. TẠO DETAILS CHO TỪNG ASSIGNMENT
                // ============================================================
                foreach (var assignment in assignmentsList)
                {
                    details.Add(new ShiftCancellationDetail(
                        ShiftId: assignment.ShiftId,
                        ShiftDate: assignment.ShiftDate,
                        ShiftTimeSlot: ShiftClassificationHelper.ClassifyShiftTimeSlot(assignment.ShiftStart),
                        ShiftStartTime: assignment.ShiftStart.TimeOfDay,
                        ShiftEndTime: assignment.ShiftEnd.TimeOfDay,
                        AssignmentsCancelled: 1, // Mỗi assignment này là 1 assignment bị cancel
                        Success: true,
                        ErrorMessage: null
                    ));
                }

                // ============================================================
                // BƯỚC 5: ⚠️ CRITICAL - PUBLISH EVENTS ĐỂ SYNC ATTENDANCES.API
                // ============================================================
                logger.LogInformation("📤 Publishing {Count} ShiftAssignmentCancelledEvent...", assignmentsList.Count);

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

                // ============================================================
                // BƯỚC 4: COMMIT TRANSACTION
                // ============================================================
                transaction.Commit();

                logger.LogInformation(
                    "✅ Bulk cancel committed: {Shifts} shifts affected, {Assignments} assignments cancelled",
                    affectedShiftIds.Count,
                    assignmentsCancelled);

                // ================================================================
                // 🆕 BƯỚC 4.5: LƯU BULK SHIFT ISSUE RECORD
                // ================================================================
                var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));

                var issueRecord = new
                {
                    Id = Guid.NewGuid(),
                    ShiftId = (Guid?)null, // Bulk cancel không có shift cụ thể
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
                    TotalShiftsAffected = affectedShiftIds.Count, // Số shifts bị ảnh hưởng
                    TotalGuardsAffected = 1, // Chỉ 1 guard (guard này)
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

                // ================================================================
                // BƯỚC 5: GỬI NOTIFICATIONS (ASYNC - NGOÀI TRANSACTION)
                // ================================================================
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

                // ================================================================
                // HOÀN THÀNH
                // ================================================================
                return new BulkCancelShiftResult(
                    Success: true,
                    Message: $"Đã hủy thành công {assignmentsCancelled} assignment(s) trong {affectedShiftIds.Count} ca trực cho bảo vệ {guard.FullName}",
                    TotalShiftsProcessed: affectedShiftIds.Count,
                    ShiftsCancelled: 0, // Không cancel shift, chỉ cancel assignments
                    AssignmentsCancelled: assignmentsCancelled,
                    GuardsAffected: 1, // Chỉ 1 guard (guard này)
                    EvidenceFileUrl: evidenceFileUrl,
                    Details: details,
                    Warnings: warnings,
                    Errors: errors
                );
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                logger.LogError(ex, "❌ Bulk cancel failed, transaction rolled back");
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error in bulk cancel shift");
            throw;
        }
    }

    /// <summary>
    /// Gửi notifications cho guard và director
    /// </summary>
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
            logger.LogInformation("📧 Sending bulk cancellation notifications...");

            // ================================================================
            // 1. GỬI EMAIL CHO GUARD
            // ================================================================
            if (!string.IsNullOrEmpty(guard.Email))
            {
                var leaveTypeName = leaveType switch
                {
                    "SICK_LEAVE" => "Nghỉ ốm",
                    "MATERNITY_LEAVE" => "Nghỉ thai sản",
                    "LONG_TERM_LEAVE" => "Nghỉ phép dài hạn",
                    _ => "Nghỉ việc"
                };

                // Tạo danh sách assignments bị hủy
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

            // ================================================================
            // 2. GỬI EMAIL BÁO CÁO CHO DIRECTOR
            // ================================================================
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

            logger.LogInformation("✓ Sent bulk cancellation report to director@basms.com");

            // ================================================================
            // 3. GỬI IN-APP NOTIFICATION CHO GUARD
            // ================================================================
            await sender.Send(new SendNotificationCommand(
                ShiftId: assignments.First().ShiftId,
                ContractId: null, // Bulk cancel không có contract cụ thể
                RecipientId: (Guid)guard.Id,
                RecipientType: "GUARD",
                Action: "BULK_SHIFT_CANCELLED",
                Title: $"{assignments.Count} ca trực đã bị hủy",
                Message: $"Các ca trực từ {assignments.Min(a => a.ShiftDate):dd/MM/yyyy} đến {assignments.Max(a => a.ShiftDate):dd/MM/yyyy} đã bị hủy. Lý do: {cancellationReason}",
                Metadata: null,
                Priority: "HIGH"
            ), cancellationToken);

            logger.LogInformation("✓ Sent in-app notification to guard");

            logger.LogInformation("✅ All notifications sent successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error sending bulk cancellation notifications");
            // Không throw exception vì notifications là optional
        }
    }
}

/// <summary>
/// DTO chứa thông tin assignment kèm guard info
/// </summary>
internal class AssignmentWithGuardInfo
{
    public Guid Id { get; set; }
    public Guid ShiftId { get; set; }
    public Guid GuardId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    // Shift info (joined from shifts table)
    public DateTime ShiftDate { get; set; }
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public string? LocationName { get; set; }
}
