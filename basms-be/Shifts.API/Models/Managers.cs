namespace Shifts.API.Models;

[Table("managers")]
public class Managers
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public string IdentityNumber { get; set; } = string.Empty;
    public DateTime? IdentityIssueDate { get; set; }
    public string? IdentityIssuePlace { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? CurrentAddress { get; set; }
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string Role { get; set; } = "MANAGER";
    public Guid? ReportsToManagerId { get; set; }
    public string? CertificationLevel { get; set; }
    public decimal? StandardWage { get; set; }
    public string? CertificationFileUrl { get; set; }
    public string? IdentityCardFrontUrl { get; set; }
    public string? IdentityCardBackUrl { get; set; }
    public string EmploymentStatus { get; set; } = "ACTIVE";
    public bool CanCreateShifts { get; set; } = true;
    public bool CanApproveShifts { get; set; } = true;
    public bool CanAssignGuards { get; set; } = true;
    public bool CanApproveOvertime { get; set; } = true;
    public bool CanManageTeams { get; set; } = true;
    public int? MaxTeamSize { get; set; }
    public int TotalTeamsManaged { get; set; } = 0;
    public int TotalGuardsSupervised { get; set; } = 0;
    public int TotalShiftsCreated { get; set; } = 0;
    public DateTime? LastSyncedAt { get; set; }
    public string SyncStatus { get; set; } = "SYNCED";
    public int? UserServiceVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }


    [Write(false)]
    [Computed]
    public virtual Managers? ReportsToManager { get; set; }
    
    [Write(false)]
    [Computed]
    public virtual ICollection<Managers> SubordinateManagers { get; set; } = new List<Managers>();

    [Write(false)]
    [Computed]
    public virtual ICollection<Teams> Teams { get; set; } = new List<Teams>();
    
    [Write(false)]
    [Computed]
    public virtual ICollection<Guards> Guards { get; set; } = new List<Guards>();
}