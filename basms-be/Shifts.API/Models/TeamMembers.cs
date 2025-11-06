using Dapper.Contrib.Extensions;

namespace Shifts.API.Models;

/// <summary>
/// TEAM_MEMBERS - Thành viên team (Many-to-Many)
/// Chức năng: Liên kết guards ↔ teams, 1 guard có thể thuộc nhiều teams
/// Use case: "Guard A thuộc Team 1 (LEADER) và Team 2 (MEMBER)"
/// </summary>
[Table("team_members")]
public class TeamMembers
{
    [ExplicitKey]
    public Guid Id { get; set; }

    public Guid TeamId { get; set; }

    /// <summary>
    /// Reference guards table
    /// </summary>
    public Guid GuardId { get; set; }

    // ============================================================================
    // VAI TRÒ
    // ============================================================================

    /// <summary>
    /// LEADER=trưởng nhóm | DEPUTY=phó | MEMBER=thành viên
    /// </summary>
    public string Role { get; set; } = "MEMBER";

    /// <summary>
    /// Còn active trong team
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ============================================================================
    // PERFORMANCE RIÊNG TRONG TEAM NÀY
    // ============================================================================

    /// <summary>
    /// 1.00-5.00 sao
    /// </summary>
    public decimal? PerformanceRating { get; set; }

    /// <summary>
    /// Số ca hoàn thành TRONG TEAM
    /// </summary>
    public int TotalShiftsCompleted { get; set; } = 0;

    /// <summary>
    /// Số ca được giao TRONG TEAM
    /// </summary>
    public int TotalShiftsAssigned { get; set; } = 0;

    /// <summary>
    /// % tham gia = (completed/assigned)×100
    /// </summary>
    public decimal? AttendanceRate { get; set; }

    // ============================================================================
    // NOTES
    // ============================================================================

    /// <summary>
    /// Ghi chú khi gia nhập: "Chuyển từ Team B"
    /// </summary>
    public string? JoiningNotes { get; set; }

    /// <summary>
    /// Ghi chú khi rời: "Xin chuyển công tác"
    /// </summary>
    public string? LeavingNotes { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Team
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Teams? Team { get; set; }

    /// <summary>
    /// Guard
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Guards? Guard { get; set; }
}