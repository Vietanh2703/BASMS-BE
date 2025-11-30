namespace Shifts.API.Models;

/// <summary>
/// SHIFT_CONFLICTS - Phát hiện xung đột tự động
/// Chức năng: Auto-detect xung đột (double booking, overtime limit, etc.)
/// Use case: "Guard A được giao 2 ca cùng lúc → CRITICAL conflict"
/// </summary>
[Table("shift_conflicts")]
public class ShiftConflicts
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // LOẠI XUNG ĐỘT
    // ============================================================================

    /// <summary>
    /// DOUBLE_BOOKING=giao 2 ca cùng lúc | INSUFFICIENT_REST=không đủ 12h nghỉ | OVERTIME_LIMIT=vượt 40h/tháng | LEAVE_OVERLAP=trùng nghỉ phép | SKILL_MISMATCH=thiếu skill
    /// </summary>
    public string ConflictType { get; set; } = string.Empty;

    /// <summary>
    /// LOW=cảnh báo | MEDIUM=cần xem xét | HIGH=cần xử lý ngay | CRITICAL=chặn assignment
    /// </summary>
    public string Severity { get; set; } = string.Empty;

    // ============================================================================
    // ENTITIES LIÊN QUAN
    // ============================================================================

    /// <summary>
    /// Guard bị conflict
    /// </summary>
    public Guid GuardId { get; set; }

    /// <summary>
    /// Ca thứ nhất
    /// </summary>
    public Guid ShiftId1 { get; set; }

    /// <summary>
    /// Ca thứ hai (cho double booking)
    /// </summary>
    public Guid? ShiftId2 { get; set; }

    /// <summary>
    /// Assignment gây conflict
    /// </summary>
    public Guid? ShiftAssignmentId { get; set; }

    // ============================================================================
    // MÔ TÁ
    // ============================================================================

    /// <summary>
    /// Chi tiết conflict
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Thời điểm phát hiện
    /// </summary>
    public DateTime DetectedAt { get; set; }

    // ============================================================================
    // RESOLUTION
    // ============================================================================

    /// <summary>
    /// OPEN=chưa xử lý | IN_PROGRESS=đang xử lý | RESOLVED=đã giải quyết | IGNORED=bỏ qua
    /// </summary>
    public string Status { get; set; } = "OPEN";

    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Manager giải quyết
    /// </summary>
    public Guid? ResolvedBy { get; set; }

    /// <summary>
    /// Cách giải quyết
    /// </summary>
    public string? ResolutionNotes { get; set; }

    // ============================================================================
    // AUTO-RESOLVE
    // ============================================================================

    /// <summary>
    /// Có thể tự động fix
    /// </summary>
    public bool AutoResolvable { get; set; } = false;

    /// <summary>
    /// Gợi ý: "Chuyển ca 2 sang guard B"
    /// </summary>
    public string? SuggestedAction { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Guard bị conflict
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Guards? Guard { get; set; }

    /// <summary>
    /// Ca thứ nhất
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Shifts? Shift1 { get; set; }

    /// <summary>
    /// Ca thứ hai
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Shifts? Shift2 { get; set; }

    /// <summary>
    /// Assignment gây conflict
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ShiftAssignments? ShiftAssignment { get; set; }

    /// <summary>
    /// Manager giải quyết
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Managers? Resolver { get; set; }
}