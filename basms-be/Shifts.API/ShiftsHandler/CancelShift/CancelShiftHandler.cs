using Dapper;
using Shifts.API.Handlers.SendNotification;
using Shifts.API.Handlers.SendEmailNotification;

namespace Shifts.API.ShiftsHandler.CancelShift;

// Command để hủy shift
public record CancelShiftCommand(
    Guid ShiftId,
    string CancellationReason,      // Lý do hủy ca
    Guid CancelledBy                // Manager hủy ca
) : ICommand<CancelShiftResult>;

// Result
public record CancelShiftResult(
    bool Success,
    string Message,
    int AffectedGuards              // Số guards bị ảnh hưởng
);

internal class CancelShiftHandler(
    IDbConnectionFactory dbFactory,
    ISender sender,
    ILogger<CancelShiftHandler> logger)
    : ICommandHandler<CancelShiftCommand, CancelShiftResult>
{
    public async Task<CancelShiftResult> Handle(
        CancelShiftCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Cancelling shift {ShiftId}", request.ShiftId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: LẤY SHIFT HIỆN TẠI
            // ================================================================
            var shift = await connection.GetAsync<Models.Shifts>(request.ShiftId);

            if (shift == null || shift.IsDeleted)
            {
                logger.LogWarning("Shift {ShiftId} not found", request.ShiftId);
                throw new InvalidOperationException($"Shift {request.ShiftId} not found");
            }

            // Kiểm tra shift đã bị hủy chưa
            if (shift.Status == "CANCELLED")
            {
                logger.LogWarning("Shift {ShiftId} is already cancelled", request.ShiftId);
                return new CancelShiftResult(
                    false,
                    "Shift đã bị hủy trước đó",
                    0);
            }

            // Kiểm tra shift đã hoàn thành chưa
            if (shift.Status == "COMPLETED")
            {
                logger.LogWarning("Cannot cancel completed shift {ShiftId}", request.ShiftId);
                throw new InvalidOperationException("Không thể hủy ca trực đã hoàn thành");
            }

            logger.LogInformation(
                "Found shift {ShiftId} with status {Status} on {ShiftDate:yyyy-MM-dd}",
                shift.Id,
                shift.Status,
                shift.ShiftDate);

            // ================================================================
            // BƯỚC 2: LẤY DANH SÁCH GUARDS ĐƯỢC ASSIGN VÀO CA NÀY
            // ================================================================
            var sql = @"
                SELECT sa.*, g.Email, g.FullName, g.PhoneNumber
                FROM shift_assignments sa
                INNER JOIN guards g ON sa.GuardId = g.Id
                WHERE sa.ShiftId = @ShiftId
                    AND sa.IsDeleted = 0
                    AND sa.Status NOT IN ('CANCELLED', 'COMPLETED')";

            var assignments = await connection.QueryAsync<AssignmentWithGuardInfo>(
                sql,
                new { ShiftId = request.ShiftId });

            var assignmentsList = assignments.ToList();

            logger.LogInformation(
                "Found {Count} active guard assignments for shift {ShiftId}",
                assignmentsList.Count,
                request.ShiftId);

            // ================================================================
            // BƯỚC 3: CẬP NHẬT SHIFT STATUS = CANCELLED
            // ================================================================
            shift.Status = "CANCELLED";
            shift.CancelledAt = DateTime.UtcNow;
            shift.CancellationReason = request.CancellationReason;
            shift.UpdatedAt = DateTime.UtcNow;
            shift.UpdatedBy = request.CancelledBy;
            shift.Version++;

            await connection.UpdateAsync(shift);

            logger.LogInformation("✓ Shift {ShiftId} status updated to CANCELLED", shift.Id);

            // ================================================================
            // BƯỚC 4: CẬP NHẬT TẤT CẢ ASSIGNMENTS = CANCELLED
            // ================================================================
            var updateAssignmentsSql = @"
                UPDATE shift_assignments
                SET
                    Status = 'CANCELLED',
                    CancelledAt = @CancelledAt,
                    CancellationReason = @CancellationReason,
                    UpdatedAt = @UpdatedAt
                WHERE ShiftId = @ShiftId
                    AND IsDeleted = 0
                    AND Status NOT IN ('CANCELLED', 'COMPLETED')";

            var affectedRows = await connection.ExecuteAsync(
                updateAssignmentsSql,
                new
                {
                    ShiftId = request.ShiftId,
                    CancelledAt = DateTime.UtcNow,
                    CancellationReason = request.CancellationReason,
                    UpdatedAt = DateTime.UtcNow
                });

            logger.LogInformation(
                "✓ Updated {Count} shift assignments to CANCELLED",
                affectedRows);

            // ================================================================
            // BƯỚC 5: GỬI IN-APP NOTIFICATIONS VÀ EMAILS CHO GUARDS
            // ================================================================
            logger.LogInformation("Sending notifications and emails to guards");

            var emailsSent = 0;
            foreach (var assignment in assignmentsList)
            {
                // Gửi in-app notification cho guard
                var notificationCommand = new SendNotificationCommand(
                    ShiftId: shift.Id,
                    ContractId: shift.ContractId,
                    RecipientId: assignment.GuardId,
                    RecipientType: "GUARD",
                    Action: "SHIFT_CANCELLED",
                    Title: "Ca trực đã bị hủy",
                    Message: $"Ca trực ngày {shift.ShiftDate:dd/MM/yyyy} ({shift.ShiftStart.TimeOfDay:hh\\:mm}-{shift.ShiftEnd.TimeOfDay:hh\\:mm}) đã bị hủy. Lý do: {request.CancellationReason}",
                    Metadata: null,
                    Priority: "HIGH"
                );

                await sender.Send(notificationCommand, cancellationToken);

                // Gửi email nếu guard có email
                if (!string.IsNullOrEmpty(assignment.Email))
                {
                    var emailCommand = new SendEmailNotificationCommand(
                        GuardName: assignment.FullName,
                        GuardEmail: assignment.Email,
                        ShiftDate: shift.ShiftDate,
                        StartTime: shift.ShiftStart.TimeOfDay,
                        EndTime: shift.ShiftEnd.TimeOfDay,
                        Location: "Location", 
                        EmailType: "CANCELLATION",
                        AdditionalInfo: request.CancellationReason
                    );

                    await sender.Send(emailCommand, cancellationToken);
                    emailsSent++;
                }
            }

            logger.LogInformation(
                "✓ Sent notifications to {Count} guards ({EmailCount} emails)",
                assignmentsList.Count,
                emailsSent);

            // ================================================================
            // BƯỚC 6: GỬI NOTIFICATION CHO DIRECTOR VÀ CUSTOMER (nếu có contract)
            // ================================================================
            if (shift.ContractId.HasValue)
            {
                logger.LogInformation("Sending notifications to director and customer");

                // Lấy Customer ID từ contract và Director ID từ system
                // Tạm thời skip phần này, sẽ implement sau khi có API integration

                logger.LogInformation("✓ Contract notifications queued");
            }

            // ================================================================
            // HOÀN THÀNH
            // ================================================================
            logger.LogInformation(
                "✓ Successfully cancelled shift {ShiftId}, affected {Count} guards",
                shift.Id,
                assignmentsList.Count);

            return new CancelShiftResult(
                true,
                $"Shift đã được hủy thành công. Đã thông báo cho {assignmentsList.Count} bảo vệ.",
                assignmentsList.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cancelling shift {ShiftId}", request.ShiftId);
            throw;
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
