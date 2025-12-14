using Dapper.Contrib.Extensions;

namespace Attendances.API.Models;

/// <summary>
/// ATTENDANCE_SUMMARY - Bảng tổng hợp chấm công theo kỳ
/// Chức năng: Pre-aggregate attendance data cho reporting nhanh
/// Use case: "Xem tổng hợp chấm công tháng 1/2025 của guard X"
/// </summary>
[Table("attendance_summary")]
public class AttendanceSummary
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // FOREIGN KEYS & SCOPE
    // ============================================================================

    /// <summary>
    /// Guard được tổng hợp
    /// </summary>
    public Guid GuardId { get; set; }

    /// <summary>
    /// Team ID (optional, for team-level reports)
    /// </summary>
    public Guid? TeamId { get; set; }

    // ============================================================================
    // TIME PERIOD
    // ============================================================================

    /// <summary>
    /// Ngày bắt đầu kỳ
    /// </summary>
    public DateTime PeriodStartDate { get; set; }

    /// <summary>
    /// Ngày kết thúc kỳ
    /// </summary>
    public DateTime PeriodEndDate { get; set; }

    /// <summary>
    /// Loại kỳ: DAILY | WEEKLY | MONTHLY | QUARTERLY | YEARLY
    /// </summary>
    public string PeriodType { get; set; } = "MONTHLY";

    /// <summary>
    /// Tháng (1-12) - for monthly summary
    /// </summary>
    public int? SummaryMonth { get; set; }

    /// <summary>
    /// Năm
    /// </summary>
    public int? SummaryYear { get; set; }

    /// <summary>
    /// Quý (1-4) - for quarterly summary
    /// </summary>
    public int? SummaryQuarter { get; set; }

    /// <summary>
    /// Tuần (1-53) - for weekly summary
    /// </summary>
    public int? SummaryWeek { get; set; }

    // ============================================================================
    // SHIFTS & ASSIGNMENTS STATISTICS
    // ============================================================================

    /// <summary>
    /// Tổng số ca được assign
    /// </summary>
    public int TotalShiftsAssigned { get; set; } = 0;

    /// <summary>
    /// Số ca đã attend (checked in)
    /// </summary>
    public int TotalShiftsAttended { get; set; } = 0;

    /// <summary>
    /// Số ca hoàn thành (checked out)
    /// </summary>
    public int TotalShiftsCompleted { get; set; } = 0;

    /// <summary>
    /// Số ca vắng mặt
    /// </summary>
    public int TotalAbsences { get; set; } = 0;

    /// <summary>
    /// Số ca đi muộn
    /// </summary>
    public int TotalLateCount { get; set; } = 0;

    /// <summary>
    /// Số ca về sớm
    /// </summary>
    public int TotalEarlyLeaveCount { get; set; } = 0;

    // ============================================================================
    // HOURS STATISTICS
    // ============================================================================

    /// <summary>
    /// Tổng giờ dự kiến (scheduled)
    /// </summary>
    public decimal TotalScheduledHours { get; set; } = 0;

    /// <summary>
    /// Tổng giờ thực tế làm việc
    /// </summary>
    public decimal TotalActualHours { get; set; } = 0;

    /// <summary>
    /// Tổng giờ OT
    /// </summary>
    public decimal TotalOvertimeHours { get; set; } = 0;

    /// <summary>
    /// Giờ làm ban ngày (6h-22h)
    /// </summary>
    public decimal TotalDayHours { get; set; } = 0;

    /// <summary>
    /// Giờ làm ban đêm (22h-6h)
    /// </summary>
    public decimal TotalNightHours { get; set; } = 0;

    // ============================================================================
    // LEAVE & ABSENCE STATISTICS
    // ============================================================================

    /// <summary>
    /// Tổng số đơn xin nghỉ approved
    /// </summary>
    public int TotalApprovedLeaves { get; set; } = 0;

    /// <summary>
    /// Số ngày nghỉ có lương
    /// </summary>
    public decimal TotalPaidLeaveDays { get; set; } = 0;

    /// <summary>
    /// Số ngày nghỉ không lương
    /// </summary>
    public decimal TotalUnpaidLeaveDays { get; set; } = 0;

    /// <summary>
    /// Số ngày nghỉ ốm
    /// </summary>
    public decimal TotalSickLeaveDays { get; set; } = 0;

    // ============================================================================
    // PUNCTUALITY METRICS
    // ============================================================================

    /// <summary>
    /// Tổng phút đi muộn
    /// </summary>
    public int TotalLateMinutes { get; set; } = 0;

    /// <summary>
    /// Tổng phút về sớm
    /// </summary>
    public int TotalEarlyLeaveMinutes { get; set; } = 0;

    /// <summary>
    /// Điểm chấm công (0-100)
    /// Calculation: (shifts_attended / shifts_assigned) * 100
    /// </summary>
    public decimal? PunctualityScore { get; set; }

    /// <summary>
    /// Tỷ lệ hoàn thành ca (%)
    /// </summary>
    public decimal? CompletionRate { get; set; }

    // ============================================================================
    // EXCEPTION & ISSUE TRACKING
    // ============================================================================

    /// <summary>
    /// Tổng số ngoại lệ chấm công
    /// </summary>
    public int TotalExceptions { get; set; } = 0;

    /// <summary>
    /// Số ngoại lệ đã giải quyết
    /// </summary>
    public int ResolvedExceptions { get; set; } = 0;

    /// <summary>
    /// Số ngoại lệ đang chờ
    /// </summary>
    public int PendingExceptions { get; set; } = 0;

    // ============================================================================
    // PERFORMANCE INDICATORS
    // ============================================================================

    /// <summary>
    /// Điểm đánh giá tổng thể (0-100)
    /// </summary>
    public decimal? PerformanceScore { get; set; }

    /// <summary>
    /// Ghi chú đánh giá
    /// </summary>
    public string? PerformanceNotes { get; set; }

    /// <summary>
    /// Đã được review bởi manager
    /// </summary>
    public bool IsReviewed { get; set; } = false;

    /// <summary>
    /// Manager review
    /// </summary>
    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    // ============================================================================
    // CALCULATION METADATA
    // ============================================================================

    /// <summary>
    /// Thời điểm tính toán summary
    /// </summary>
    public DateTime CalculatedAt { get; set; }

    /// <summary>
    /// Phiên bản tính toán (để track changes in calculation logic)
    /// </summary>
    public int CalculationVersion { get; set; } = 1;

    /// <summary>
    /// Tự động tính toán hay manual
    /// </summary>
    public bool IsAutoCalculated { get; set; } = true;

    /// <summary>
    /// Summary đã được finalize (lock changes)
    /// </summary>
    public bool IsFinalized { get; set; } = false;

    public DateTime? FinalizedAt { get; set; }

    public Guid? FinalizedBy { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

}
