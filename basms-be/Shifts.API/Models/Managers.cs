using Dapper.Contrib.Extensions;

namespace Shifts.API.Models;

/// <summary>
/// MANAGERS - Cache thông tin managers từ User Service
/// Mục đích: Giảm 80-90% API calls, tăng performance 4-10x, hoạt động độc lập
/// </summary>
[Table("managers")]
public class Managers
{
    /// <summary>
    /// Trùng với User Service user_id
    /// </summary>
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // THÔNG TIN CƠ BẢN (sync từ User Service)
    // ============================================================================

    /// <summary>
    /// Số CCCD
    /// </summary>
    public string IdentityNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Mã NV: MGR001
    /// </summary>
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>
    /// Họ tên đầy đủ tiếng Việt
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Link ảnh đại diện
    /// </summary>
    public string? AvatarUrl { get; set; }
    
    /// <summary>
    /// Email đăng nhập
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// SĐT liên hệ
    /// </summary>
    public string? PhoneNumber { get; set; }
    
    /// <summary>
    /// Địa chỉ hiện tại
    /// </summary>
    public string? CurrentAddress { get; set; }
    
    /// <summary>
    /// MALE | FEMALE
    /// </summary>
    public string? Gender { get; set; }
    
    public DateTime? DateOfBirth { get; set; }

    // ============================================================================
    // VAI TRÒ & CHỨC VỤ
    // ============================================================================

    /// <summary>
    /// MANAGER| DIRECTOR
    /// </summary>
    public string Role { get; set; } = "MANAGER";

    /// <summary>
    /// Chức danh: "Trưởng phòng vận hành"
    /// </summary>
    public string? Position { get; set; }

    /// <summary>
    /// Phòng ban
    /// </summary>
    public string? Department { get; set; }

    // ============================================================================
    // CẤP BẬC QUẢN LÝ (hierarchical)
    // ============================================================================

    /// <summary>
    /// 1=Line Manager (10-30 guards) | 2=Senior (3-5 managers) | 3=Director (C-level)
    /// </summary>
    public int ManagerLevel { get; set; } = 1;

    /// <summary>
    /// Manager cấp trên (NULL=Director)
    /// </summary>
    public Guid? ReportsToManagerId { get; set; }

    // ============================================================================
    // TÌNH TRẠNG LÀM VIỆC
    // ============================================================================

    /// <summary>
    /// ACTIVE=đang làm | ON_LEAVE=nghỉ dài hạn | SUSPENDED=đình chỉ | TERMINATED=đã nghỉ
    /// </summary>
    public string EmploymentStatus { get; set; } = "ACTIVE";

    // ============================================================================
    // PHÂN QUYỀN TRONG SERVICE NÀY (không sync từ User)
    // ============================================================================

    /// <summary>
    /// Được tạo ca trực mới
    /// </summary>
    public bool CanCreateShifts { get; set; } = true;

    /// <summary>
    /// Được duyệt ca trực
    /// </summary>
    public bool CanApproveShifts { get; set; } = true;

    /// <summary>
    /// Được phân công guards
    /// </summary>
    public bool CanAssignGuards { get; set; } = true;

    /// <summary>
    /// Được duyệt tăng ca
    /// </summary>
    public bool CanApproveOvertime { get; set; } = true;

    /// <summary>
    /// Được quản lý teams
    /// </summary>
    public bool CanManageTeams { get; set; } = true;

    /// <summary>
    /// Giới hạn số guards quản lý (VD: 50)
    /// </summary>
    public int? MaxTeamSize { get; set; }

    // ============================================================================
    // THỐNG KÊ (auto-calculated)
    // ============================================================================

    /// <summary>
    /// Tổng team đang quản lý
    /// </summary>
    public int TotalTeamsManaged { get; set; } = 0;

    /// <summary>
    /// Tổng guards dưới quyền
    /// </summary>
    public int TotalGuardsSupervised { get; set; } = 0;

    /// <summary>
    /// Tổng ca đã tạo
    /// </summary>
    public int TotalShiftsCreated { get; set; } = 0;
    

    // ============================================================================
    // SYNC METADATA (quan trọng!)
    // ============================================================================

    /// <summary>
    /// Lần sync cuối - phát hiện stale data
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// SYNCED=OK | PENDING=đang sync | FAILED=lỗi
    /// </summary>
    public string SyncStatus { get; set; } = "SYNCED";

    /// <summary>
    /// Version từ User Service - detect changes
    /// </summary>
    public int? UserServiceVersion { get; set; }

    /// <summary>
    /// Còn hoạt động (soft delete)
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // ============================================================================
    // NAVIGATION PROPERTIES
    // ============================================================================

    /// <summary>
    /// Manager cấp trên
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual Managers? ReportsToManager { get; set; }

    /// <summary>
    /// Các managers cấp dưới
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<Managers> SubordinateManagers { get; set; } = new List<Managers>();

    /// <summary>
    /// Teams do manager này quản lý
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<Teams> Teams { get; set; } = new List<Teams>();

    /// <summary>
    /// Guards được giám sát trực tiếp
    /// </summary>
    [Write(false)]
    [Computed]
    public virtual ICollection<Guards> Guards { get; set; } = new List<Guards>();
}