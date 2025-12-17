using Dapper;
using MassTransit;
using BuildingBlocks.Messaging.Events;
using Shifts.API.Handlers.SendNotification;
using Shifts.API.Handlers.SendEmailNotification;
using Shifts.API.Helpers;

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
    string? EvidenceImageUrl,
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
            // BƯỚC 2: TÌM TẤT CẢ SHIFTS CỦA GUARD TRONG DATE RANGE
            // ================================================================
            var shiftsQuery = @"
                SELECT DISTINCT s.*
                FROM shifts s
                INNER JOIN shift_assignments sa ON sa.ShiftId = s.Id
                WHERE sa.GuardId = @GuardId
                  AND s.ShiftDate >= @FromDate
                  AND s.ShiftDate <= @ToDate
                  AND s.IsDeleted = 0
                  AND sa.IsDeleted = 0
                  AND s.Status NOT IN ('CANCELLED', 'COMPLETED')
                  AND sa.Status NOT IN ('CANCELLED', 'COMPLETED')
                ORDER BY s.ShiftDate, s.ShiftStart";

            var shifts = await connection.QueryAsync<Models.Shifts>(
                shiftsQuery,
                new
                {
                    GuardId = request.GuardId,
                    FromDate = request.FromDate.Date,
                    ToDate = request.ToDate.Date
                });

            var shiftsList = shifts.ToList();

            if (!shiftsList.Any())
            {
                logger.LogWarning("⚠️ No shifts found for Guard {GuardId} in date range", request.GuardId);
                return new BulkCancelShiftResult(
                    Success: true,
                    Message: "Không tìm thấy ca trực nào cần hủy trong khoảng thời gian này",
                    TotalShiftsProcessed: 0,
                    ShiftsCancelled: 0,
                    AssignmentsCancelled: 0,
                    GuardsAffected: 0,
                    Details: new List<ShiftCancellationDetail>(),
                    Warnings: new List<string>(),
                    Errors: new List<string>()
                );
            }

            logger.LogInformation(
                "✓ Found {Count} shifts to cancel",
                shiftsList.Count);

            // ================================================================
            // BƯỚC 3: LẤY TẤT CẢ ASSIGNMENTS CẦN CANCEL
            // ================================================================
            var shiftIds = shiftsList.Select(s => s.Id).ToList();

            var assignmentsQuery = @"
                SELECT sa.*, g.Email, g.FullName, g.PhoneNumber
                FROM shift_assignments sa
                INNER JOIN guards g ON sa.GuardId = g.Id
                WHERE sa.ShiftId IN @ShiftIds
                  AND sa.IsDeleted = 0
                  AND sa.Status NOT IN ('CANCELLED', 'COMPLETED')";

            var assignments = await connection.QueryAsync<AssignmentWithGuardInfo>(
                assignmentsQuery,
                new { ShiftIds = shiftIds });

            var assignmentsList = assignments.ToList();

            logger.LogInformation(
                "✓ Found {Count} assignments across all shifts",
                assignmentsList.Count);

            // Tính số guards bị ảnh hưởng
            var affectedGuardIds = assignmentsList.Select(a => a.GuardId).Distinct().ToList();

            // ================================================================
            // BƯỚC 4: BEGIN TRANSACTION - BULK UPDATE DATABASE
            // ================================================================
            using var transaction = connection.BeginTransaction();

            try
            {
                int shiftsCancelled = 0;
                int assignmentsCancelled = 0;
                var details = new List<ShiftCancellationDetail>();
                var warnings = new List<string>();
                var errors = new List<string>();

                // ============================================================
                // 4.1. BULK UPDATE SHIFTS
                // ============================================================
                var updateShiftsSql = @"
                    UPDATE shifts
                    SET
                        Status = 'CANCELLED',
                        CancelledAt = @CancelledAt,
                        CancellationReason = @CancellationReason,
                        UpdatedAt = @UpdatedAt,
                        UpdatedBy = @UpdatedBy,
                        Version = Version + 1
                    WHERE Id IN @ShiftIds
                      AND IsDeleted = 0
                      AND Status NOT IN ('CANCELLED', 'COMPLETED')";

                shiftsCancelled = await connection.ExecuteAsync(
                    updateShiftsSql,
                    new
                    {
                        ShiftIds = shiftIds,
                        CancelledAt = DateTime.UtcNow,
                        CancellationReason = request.CancellationReason,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = request.CancelledBy
                    },
                    transaction);

                logger.LogInformation("✓ Updated {Count} shifts to CANCELLED", shiftsCancelled);

                // ============================================================
                // 4.2. BULK UPDATE ASSIGNMENTS
                // ============================================================
                var updateAssignmentsSql = @"
                    UPDATE shift_assignments
                    SET
                        Status = 'CANCELLED',
                        CancelledAt = @CancelledAt,
                        CancellationReason = @CancellationReason,
                        UpdatedAt = @UpdatedAt
                    WHERE ShiftId IN @ShiftIds
                      AND IsDeleted = 0
                      AND Status NOT IN ('CANCELLED', 'COMPLETED')";

                assignmentsCancelled = await connection.ExecuteAsync(
                    updateAssignmentsSql,
                    new
                    {
                        ShiftIds = shiftIds,
                        CancelledAt = DateTime.UtcNow,
                        CancellationReason = request.CancellationReason,
                        UpdatedAt = DateTime.UtcNow
                    },
                    transaction);

                logger.LogInformation("✓ Updated {Count} assignments to CANCELLED", assignmentsCancelled);

                // ============================================================
                // 4.3. TẠO DETAILS CHO TỪNG SHIFT
                // ============================================================
                foreach (var shift in shiftsList)
                {
                    var assignmentsForShift = assignmentsList.Count(a => a.ShiftId == shift.Id);

                    details.Add(new ShiftCancellationDetail(
                        ShiftId: shift.Id,
                        ShiftDate: shift.ShiftDate,
                        ShiftTimeSlot: ShiftClassificationHelper.ClassifyShiftTimeSlot(shift.ShiftStart),
                        ShiftStartTime: shift.ShiftStart.TimeOfDay,
                        ShiftEndTime: shift.ShiftEnd.TimeOfDay,
                        AssignmentsCancelled: assignmentsForShift,
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
                        EvidenceImageUrl = request.EvidenceImageUrl
                    }, cancellationToken);
                }

                logger.LogInformation(
                    "✓ Published {Count} events to sync with Attendances.API",
                    assignmentsList.Count);

                // ============================================================
                // BƯỚC 6: COMMIT TRANSACTION
                // ============================================================
                transaction.Commit();

                logger.LogInformation(
                    "✅ Bulk cancel committed: {Shifts} shifts, {Assignments} assignments",
                    shiftsCancelled,
                    assignmentsCancelled);

                // ================================================================
                // 🆕 BƯỚC 6.5: LƯU BULK SHIFT ISSUE RECORD
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
                    EvidenceFileUrl = request.EvidenceImageUrl,
                    TotalShiftsAffected = shiftsCancelled,
                    TotalGuardsAffected = affectedGuardIds.Count,
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
                    "✓ Saved bulk shift issue record: {IssueId}, Type: {IssueType}, Shifts: {Shifts}, Guard: {GuardName}",
                    issueRecord.Id,
                    issueRecord.IssueType,
                    issueRecord.TotalShiftsAffected,
                    (string)guard.FullName);

                // ================================================================
                // BƯỚC 7: GỬI NOTIFICATIONS (ASYNC - NGOÀI TRANSACTION)
                // ================================================================
                _ = Task.Run(async () =>
                {
                    await SendBulkCancellationNotifications(
                        guard,
                        assignmentsList,
                        shiftsList,
                        request.CancellationReason,
                        request.LeaveType,
                        request.EvidenceImageUrl,
                        cancellationToken);
                }, cancellationToken);

                // ================================================================
                // HOÀN THÀNH
                // ================================================================
                return new BulkCancelShiftResult(
                    Success: true,
                    Message: $"Đã hủy thành công {shiftsCancelled} ca trực cho bảo vệ {guard.FullName}",
                    TotalShiftsProcessed: shiftsList.Count,
                    ShiftsCancelled: shiftsCancelled,
                    AssignmentsCancelled: assignmentsCancelled,
                    GuardsAffected: affectedGuardIds.Count,
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
        List<Models.Shifts> shifts,
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

                // Tạo danh sách ca bị hủy
                var shiftList = string.Join("\n", shifts.Select(s =>
                    $"- Ngày {s.ShiftDate:dd/MM/yyyy}: {ShiftClassificationHelper.ClassifyShiftTimeSlot(s.ShiftStart)} ({s.ShiftStart.TimeOfDay:hh\\:mm}-{s.ShiftEnd.TimeOfDay:hh\\:mm})"));

                var emailBody = $@"
{leaveTypeName} từ {shifts.Min(s => s.ShiftDate):dd/MM/yyyy} đến {shifts.Max(s => s.ShiftDate):dd/MM/yyyy}|
Lý do: {cancellationReason}|
Số ca bị hủy: {shifts.Count}|
{shiftList}|
{evidenceImageUrl ?? ""}";

                await sender.Send(new SendEmailNotificationCommand(
                    GuardName: guard.FullName,
                    GuardEmail: guard.Email,
                    ShiftDate: shifts.Min(s => s.ShiftDate),
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

            var directorEmailBody = $@"
Báo cáo: Bảo vệ {guard.FullName} (#{guard.EmployeeCode}) {leaveTypeDisplay}|
Thời gian nghỉ: {shifts.Min(s => s.ShiftDate):dd/MM/yyyy} - {shifts.Max(s => s.ShiftDate):dd/MM/yyyy}|
Lý do: {cancellationReason}|
Số ca bị hủy: {shifts.Count} ca|
Số bảo vệ khác bị ảnh hưởng: {assignments.Select(a => a.GuardId).Distinct().Count() - 1}|
{evidenceImageUrl ?? ""}";

            await sender.Send(new SendEmailNotificationCommand(
                GuardName: "Director",
                GuardEmail: "director@basms.com",
                ShiftDate: shifts.Min(s => s.ShiftDate),
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
                ShiftId: shifts.First().Id,
                ContractId: shifts.First().ContractId,
                RecipientId: (Guid)guard.Id,
                RecipientType: "GUARD",
                Action: "BULK_SHIFT_CANCELLED",
                Title: $"{shifts.Count} ca trực đã bị hủy",
                Message: $"Các ca trực từ {shifts.Min(s => s.ShiftDate):dd/MM/yyyy} đến {shifts.Max(s => s.ShiftDate):dd/MM/yyyy} đã bị hủy. Lý do: {cancellationReason}",
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
}
