namespace Shifts.API.Models;

[Table("shift_change_requests")]
public class ShiftChangeRequests
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid ShiftAssignmentId { get; set; }

    /// <summary>
    /// Guard xin đổi
    /// </summary>
    public Guid RequestingGuardId { get; set; }

    // ============================================================================
    // LOẠI REQUEST
    // ============================================================================

    /// <summary>
    /// SWAP=hoán ca | DROP=bỏ ca | TAKE_OVER=nhận ca thêm | TIME_CHANGE=đổi giờ
    /// </summary>
    public string RequestType { get; set; } = string.Empty;

    // ============================================================================
    // CHO SWAP (hoán ca với guard khác)
    // ============================================================================

    /// <summary>
    /// Đổi với guard nào
    /// </summary>
    public Guid? TargetGuardId { get; set; }

    /// <summary>
    /// Ca của guard kia
    /// </summary>
    public Guid? TargetShiftAssignmentId { get; set; }

    // ============================================================================
    // REQUEST DETAILS
    // ============================================================================

    /// <summary>
    /// Lý do: "Bận việc gia đình"
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; }

    // ============================================================================
    // APPROVAL WORKFLOW
    // ============================================================================

    /// <summary>
    /// PENDING=chờ duyệt | APPROVED=đã duyệt | REJECTED=từ chối | CANCELLED=hủy
    /// </summary>
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// Manager review
    /// </summary>
    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Nhận xét của manager
    /// </summary>
    public string? ReviewNotes { get; set; }

    /// <summary>
    /// Có hiệu lực từ khi nào
    /// </summary>
    public DateTime? EffectiveFrom { get; set; }

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
    /// Assignment muốn đổi
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ShiftAssignments? ShiftAssignment { get; set; }

    /// <summary>
    /// Guard yêu cầu
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Guards? RequestingGuard { get; set; }

    /// <summary>
    /// Guard đích (cho SWAP)
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Guards? TargetGuard { get; set; }

    /// <summary>
    /// Assignment đích (cho SWAP)
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ShiftAssignments? TargetShiftAssignment { get; set; }

    /// <summary>
    /// Manager duyệt
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Managers? Reviewer { get; set; }
}