using Dapper;
using MassTransit;
using BuildingBlocks.Messaging.Events;
using Attendances.API.Helpers;
using Attendances.API.Models;

namespace Attendances.API.Consumers;

/// <summary>
/// Consumer để tự động cập nhật AttendanceRecord khi ShiftAssignment bị cancel
///
/// WORKFLOW:
/// 1. Shifts.API cancel assignment → Publish ShiftAssignmentCancelledEvent
/// 2. Attendances.API nhận event
/// 3. Tìm AttendanceRecord tương ứng
/// 4. Xử lý theo 3 trường hợp:
///    - CASE 1: Chưa check-in → Soft delete (Status = CANCELLED, IsDeleted = true)
///    - CASE 2: Đã check-in nhưng chưa check-out → Mark INCOMPLETE + flag for review
///    - CASE 3: Đã check-out → Giữ nguyên record + thêm note
/// </summary>
public class CancelAttendanceRecordConsumer : IConsumer<ShiftAssignmentCancelledEvent>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<CancelAttendanceRecordConsumer> _logger;

    public CancelAttendanceRecordConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<CancelAttendanceRecordConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ShiftAssignmentCancelledEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "📨 Received ShiftAssignmentCancelledEvent: Assignment={AssignmentId}, Guard={GuardId}, Reason={Reason}, LeaveType={LeaveType}",
            message.ShiftAssignmentId,
            message.GuardId,
            message.CancellationReason,
            message.LeaveType);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            // ================================================================
            // TÌM ATTENDANCE RECORD TƯƠNG ỨNG
            // ================================================================
            var record = await connection.QueryFirstOrDefaultAsync<AttendanceRecords>(
                @"SELECT * FROM attendance_records
                  WHERE ShiftAssignmentId = @ShiftAssignmentId
                    AND IsDeleted = 0",
                new { message.ShiftAssignmentId });

            if (record == null)
            {
                _logger.LogWarning(
                    "⚠️ No attendance record found for ShiftAssignment={AssignmentId}, skipping update",
                    message.ShiftAssignmentId);
                return;
            }

            _logger.LogInformation(
                "✓ Found attendance record {RecordId} (Status: {Status}, CheckIn: {CheckIn}, CheckOut: {CheckOut})",
                record.Id,
                record.Status,
                record.CheckInTime?.ToString("yyyy-MM-dd HH:mm") ?? "null",
                record.CheckOutTime?.ToString("yyyy-MM-dd HH:mm") ?? "null");

            // ================================================================
            // LOGIC PHÂN LOẠI XỬ LÝ
            // ================================================================

            var leaveTypeDisplay = message.LeaveType switch
            {
                "SICK_LEAVE" => "nghỉ ốm",
                "MATERNITY_LEAVE" => "nghỉ thai sản",
                "LONG_TERM_LEAVE" => "nghỉ phép dài hạn",
                _ => "nghỉ việc"
            };

            if (record.CheckInTime == null)
            {
                // ================================================================
                // CASE 1: CHƯA CHECK-IN → SOFT DELETE
                // ================================================================
                _logger.LogInformation(
                    "  → Case 1: No check-in yet, soft deleting attendance record");

                record.Status = "CANCELLED";
                record.IsDeleted = true;
                record.DeletedAt = DateTimeHelper.VietnamNow;
                record.ManagerNotes = $"Ca trực bị hủy do {leaveTypeDisplay}: {message.CancellationReason}";
                record.UpdatedAt = DateTimeHelper.VietnamNow;
            }
            else if (record.CheckOutTime == null)
            {
                // ================================================================
                // CASE 2: ĐÃ CHECK-IN NHƯNG CHƯA CHECK-OUT → MARK INCOMPLETE
                // ================================================================
                _logger.LogInformation(
                    "  → Case 2: Checked in but not out, marking as incomplete");

                record.Status = "INCOMPLETE";
                record.IsIncomplete = true;
                record.FlagsForReview = true;
                record.FlagReason = $"Ca trực bị hủy giữa chừng do {leaveTypeDisplay}";
                record.ManagerNotes = $"⚠️ Guard đã check-in lúc {record.CheckInTime:HH:mm} nhưng ca bị hủy do {leaveTypeDisplay}: {message.CancellationReason}. Cần verify lại.";
                record.UpdatedAt = DateTimeHelper.VietnamNow;

                _logger.LogWarning(
                    "⚠️ Guard already checked in at {CheckInTime}, marking as INCOMPLETE for review",
                    record.CheckInTime);
            }
            else
            {
                // ================================================================
                // CASE 3: ĐÃ CHECK-OUT → GIỮ NGUYÊN + NOTE
                // ================================================================
                _logger.LogInformation(
                    "  → Case 3: Already checked out, keeping record with note");

                record.ManagerNotes = $"ℹ️ Ca trực đã hoàn thành (check-out lúc {record.CheckOutTime:HH:mm}) nhưng sau đó bị hủy do {leaveTypeDisplay}: {message.CancellationReason}";
                record.UpdatedAt = DateTimeHelper.VietnamNow;

                _logger.LogInformation(
                    "ℹ️ Guard already checked out at {CheckOutTime}, keeping record intact",
                    record.CheckOutTime);
            }

            // ================================================================
            // UPDATE DATABASE
            // ================================================================
            var updateSql = @"
                UPDATE attendance_records
                SET Status = @Status,
                    IsIncomplete = @IsIncomplete,
                    FlagsForReview = @FlagsForReview,
                    FlagReason = @FlagReason,
                    ManagerNotes = @ManagerNotes,
                    IsDeleted = @IsDeleted,
                    DeletedAt = @DeletedAt,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            await connection.ExecuteAsync(updateSql, record);

            _logger.LogInformation(
                "✅ Updated attendance record {RecordId} → Status={Status}, IsDeleted={IsDeleted}",
                record.Id,
                record.Status,
                record.IsDeleted);

            // Log chi tiết cho bulk cancellation
            if (!string.IsNullOrEmpty(message.EvidenceImageUrl))
            {
                _logger.LogInformation(
                    "   Evidence image: {EvidenceUrl}",
                    message.EvidenceImageUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Error updating attendance record for ShiftAssignment={AssignmentId}",
                message.ShiftAssignmentId);

            // Throw để MassTransit retry
            throw;
        }
    }
}
