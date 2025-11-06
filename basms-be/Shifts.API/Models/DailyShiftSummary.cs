namespace Shifts.API.Models;

/// <summary>
/// DAILY_SHIFT_SUMMARY - Tổng hợp ngày
/// Chức năng: Pre-aggregated metrics cho dashboard nhanh
/// Use case: Dashboard hiển thị tổng quan 1 ngày (không cần scan toàn bộ shifts)
/// </summary>
[Table("daily_shift_summary")]
public class DailyShiftSummary
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // DATE COMPONENTS (split để query nhanh)
    // ============================================================================

    /// <summary>
    /// Ngày tổng hợp
    /// </summary>
    public DateTime SummaryDate { get; set; }

    public int SummaryDay { get; set; }
    public int SummaryMonth { get; set; }
    public int SummaryYear { get; set; }
    public int SummaryQuarter { get; set; }
    public int SummaryWeek { get; set; }
    public int DayOfWeek { get; set; }

    // ============================================================================
    // OPTIONAL GROUPING
    // ============================================================================

    /// <summary>
    /// NULL=toàn công ty
    /// </summary>
    public Guid? LocationId { get; set; }

    /// <summary>
    /// NULL=tất cả teams
    /// </summary>
    public Guid? TeamId { get; set; }

    // ============================================================================
    // DAY CLASSIFICATION
    // ============================================================================

    public bool IsWeekend { get; set; } = false;
    public bool IsPublicHoliday { get; set; } = false;
    public bool IsTetHoliday { get; set; } = false;

    /// <summary>
    /// Tên lễ: "Tết Nguyên Đán"
    /// </summary>
    public string? HolidayName { get; set; }

    // ============================================================================
    // SHIFT COUNTS
    // ============================================================================

    /// <summary>
    /// Tổng ca đã lên lịch
    /// </summary>
    public int TotalShiftsScheduled { get; set; } = 0;

    /// <summary>
    /// Ca đang diễn ra
    /// </summary>
    public int TotalShiftsInProgress { get; set; } = 0;

    /// <summary>
    /// Ca hoàn thành
    /// </summary>
    public int TotalShiftsCompleted { get; set; } = 0;

    /// <summary>
    /// Ca bị hủy
    /// </summary>
    public int TotalShiftsCancelled { get; set; } = 0;

    // ============================================================================
    // GUARD COUNTS
    // ============================================================================

    /// <summary>
    /// Tổng guards cần
    /// </summary>
    public int TotalGuardsRequired { get; set; } = 0;

    /// <summary>
    /// Tổng guards đã giao
    /// </summary>
    public int TotalGuardsAssigned { get; set; } = 0;

    /// <summary>
    /// Guards xác nhận
    /// </summary>
    public int TotalGuardsConfirmed { get; set; } = 0;

    /// <summary>
    /// Guards đã check-in
    /// </summary>
    public int TotalGuardsCheckedIn { get; set; } = 0;

    /// <summary>
    /// Guards hoàn thành
    /// </summary>
    public int TotalGuardsCompleted { get; set; } = 0;

    /// <summary>
    /// Guards không đến
    /// </summary>
    public int TotalGuardsNoShow { get; set; } = 0;

    /// <summary>
    /// Guards vắng mặt
    /// </summary>
    public int TotalGuardsAbsent { get; set; } = 0;

    // ============================================================================
    // HOURS SUMMARY
    // ============================================================================

    /// <summary>
    /// Tổng giờ theo lịch
    /// </summary>
    public decimal TotalScheduledHours { get; set; } = 0;

    /// <summary>
    /// Giờ bình thường
    /// </summary>
    public decimal TotalRegularHours { get; set; } = 0;

    /// <summary>
    /// Giờ tăng ca
    /// </summary>
    public decimal TotalOvertimeHours { get; set; } = 0;

    /// <summary>
    /// Giờ đêm (22h-6h)
    /// </summary>
    public decimal TotalNightHours { get; set; } = 0;

    // ============================================================================
    // STAFFING RATE
    // ============================================================================

    /// <summary>
    /// Trung bình % đủ người = AVG(assigned/required)×100
    /// </summary>
    public decimal? AverageStaffingRate { get; set; }

    // ============================================================================
    // CALCULATION METADATA
    // ============================================================================

    /// <summary>
    /// Lần tính cuối
    /// </summary>
    public DateTime? LastCalculatedAt { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Team (nếu summary theo team)
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Teams? Team { get; set; }
}