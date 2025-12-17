using Dapper.Contrib.Extensions;

namespace Shifts.API.Models;

/// <summary>
/// SHIFT_ISSUES - Bảng lưu thông tin sự cố ảnh hưởng tới ca trực và nhân sự
///
/// USE CASES:
/// - Cancel shift đơn lẻ
/// - Bulk cancel shifts (nghỉ ốm dài ngày, thai sản, nghỉ phép dài hạn)
/// - Các sự cố khác ảnh hưởng tới ca trực hoặc nhân sự
///
/// EXAMPLES:
/// 1. Guard nghỉ thai sản 3 tháng:
///    - IssueType: MATERNITY_LEAVE
///    - StartDate: 2025-02-01
///    - EndDate: 2025-04-30
///    - TotalShiftsAffected: 90
///    - EvidenceFileUrl: S3 URL giấy thai sản
///
/// 2. Cancel shift đơn lẻ do thay đổi lịch:
///    - IssueType: CANCEL_SHIFT
///    - ShiftId: shift-id-xxx
///    - TotalShiftsAffected: 1
/// </summary>
[Table("shift_issues")]
public class ShiftIssues
{
    /// <summary>
    /// Primary key
    /// </summary>
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Shift bị ảnh hưởng (NULL nếu là bulk cancel nhiều shifts)
    /// </summary>
    public Guid? ShiftId { get; set; }

    /// <summary>
    /// Guard liên quan đến issue (NULL nếu không liên quan đến guard cụ thể)
    /// </summary>
    public Guid? GuardId { get; set; }

    /// <summary>
    /// Loại sự cố:
    /// - CANCEL_SHIFT: Hủy ca đơn lẻ
    /// - BULK_CANCEL: Hủy nhiều ca (generic)
    /// - SICK_LEAVE: Nghỉ ốm dài ngày
    /// - MATERNITY_LEAVE: Nghỉ thai sản
    /// - LONG_TERM_LEAVE: Nghỉ phép dài hạn
    /// - OTHER: Loại khác
    /// </summary>
    public string IssueType { get; set; } = string.Empty;

    /// <summary>
    /// Lý do chi tiết (nghỉ ốm, thai sản, thay đổi lịch, v.v.)
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Ngày bắt đầu nghỉ (cho trường hợp nghỉ dài ngày)
    /// NULL nếu là cancel shift đơn lẻ
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Ngày kết thúc nghỉ (cho trường hợp nghỉ dài ngày)
    /// NULL nếu là cancel shift đơn lẻ
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Ngày phát sinh sự cố (Vietnam timezone)
    /// </summary>
    public DateTime IssueDate { get; set; }

    /// <summary>
    /// URL file chứng từ trên AWS S3
    /// (đơn xin nghỉ, giấy khám bệnh, giấy thai sản, v.v.)
    /// </summary>
    public string? EvidenceFileUrl { get; set; }

    /// <summary>
    /// Tổng số ca bị ảnh hưởng
    /// </summary>
    public int TotalShiftsAffected { get; set; } = 0;

    /// <summary>
    /// Tổng số guard bị ảnh hưởng
    /// </summary>
    public int TotalGuardsAffected { get; set; } = 0;

    // ============================================================================
    // AUDIT
    // ============================================================================

    /// <summary>
    /// Thời gian tạo record (Vietnam timezone)
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Manager tạo record
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Thời gian cập nhật cuối (Vietnam timezone)
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Người cập nhật cuối
    /// </summary>
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Thời gian xóa (Vietnam timezone)
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Người xóa
    /// </summary>
    public Guid? DeletedBy { get; set; }
}
