namespace Contracts.API.Models;

/// <summary>
/// LOG ĐỒNG BỘ ATTENDANCE
/// Tracking sync data từ Attendance Service
/// Ghi lại số lượng check-ins/check-outs theo từng địa điểm và ngày
/// </summary>
[Table("attendance_sync_log")]
public class AttendanceSyncLog
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Địa điểm nào
    /// </summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// Ngày đồng bộ dữ liệu
    /// </summary>
    public DateTime SyncDate { get; set; }

    /// <summary>
    /// Tổng số ca trong ngày
    /// </summary>
    public int? TotalShifts { get; set; }

    /// <summary>
    /// Tổng số lần check-in
    /// </summary>
    public int? TotalCheckIns { get; set; }

    /// <summary>
    /// Tổng số lần check-out
    /// </summary>
    public int? TotalCheckOuts { get; set; }

    /// <summary>
    /// Trạng thái đồng bộ: pending, completed, failed
    /// </summary>
    public string SyncStatus { get; set; } = "pending";

    /// <summary>
    /// Thời điểm đồng bộ thành công
    /// </summary>
    public DateTime? SyncedAt { get; set; }

    public string? Notes { get; set; }
}
