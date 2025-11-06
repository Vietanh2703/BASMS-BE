using Dapper.Contrib.Extensions;

namespace Shifts.API.Models;

/// <summary>
/// TEAMS - Đội nhóm bảo vệ
/// Chức năng: Tổ chức guards thành đội, phân công theo chuyên môn
/// Use case: "Team A: 10 guards, chuyên khu dân cư, ca ngày"
/// </summary>
[Table("teams")]
public class Teams
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Manager quản lý team
    /// </summary>
    public Guid ManagerId { get; set; }

    // ============================================================================
    // IDENTITY
    // ============================================================================

    /// <summary>
    /// Mã: TEAM-A, COMMERCIAL-DAY-01
    /// </summary>
    public string TeamCode { get; set; } = string.Empty;

    /// <summary>
    /// Tên: "Đội Bảo Vệ Trung Tâm A"
    /// </summary>
    public string TeamName { get; set; } = string.Empty;

    /// <summary>
    /// Mô tả team
    /// </summary>
    public string? Description { get; set; }

    // ============================================================================
    // CAPACITY
    // ============================================================================

    /// <summary>
    /// Số guards tối thiểu để hoạt động
    /// </summary>
    public int MinMembers { get; set; } = 1;

    /// <summary>
    /// Số guards tối đa (giới hạn quản lý)
    /// </summary>
    public int? MaxMembers { get; set; }

    /// <summary>
    /// Số guards hiện tại (auto-update)
    /// </summary>
    public int CurrentMemberCount { get; set; } = 0;

    // ============================================================================
    // CHUYÊN MÔN
    // ============================================================================

    /// <summary>
    /// RESIDENTIAL=dân cư | COMMERCIAL=văn phòng | EVENT=sự kiện | VIP=cá nhân | INDUSTRIAL=nhà máy
    /// </summary>
    public string? Specialization { get; set; }

    public bool IsActive { get; set; } = true;

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Manager tạo team
    /// </summary>
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Manager quản lý team
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Managers? Manager { get; set; }

    /// <summary>
    /// Danh sách thành viên team
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<TeamMembers> Members { get; set; } = new List<TeamMembers>();

    /// <summary>
    /// Shift assignments của team
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<ShiftAssignments> ShiftAssignments { get; set; } = new List<ShiftAssignments>();
}