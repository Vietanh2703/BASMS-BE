namespace Shifts.API.Models;

[Table("guards")]
public class Guards
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public string IdentityNumber { get; set; } = string.Empty;
    public DateTime? IdentityIssueDate { get; set; }
    public string? IdentityIssuePlace { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Email { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? CurrentAddress { get; set; }
    public string EmploymentStatus { get; set; } = "ACTIVE";
    public DateTime HireDate { get; set; }
    public DateTime? ProbationEndDate { get; set; }
    public string? ContractType { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public Guid? DirectManagerId { get; set; }
    public string? CertificationLevel { get; set; }
    public decimal? StandardWage { get; set; }
    public string? CertificationFileUrl { get; set; }
    public string? IdentityCardFrontUrl { get; set; }
    public string? IdentityCardBackUrl { get; set; }
    public string? PreferredShiftType { get; set; }
    public string? PreferredLocations { get; set; }
    public int MaxWeeklyHours { get; set; } = 48;
    public bool CanWorkOvertime { get; set; } = true;
    public bool CanWorkWeekends { get; set; } = true;
    public bool CanWorkHolidays { get; set; } = true;
    public int TotalShiftsWorked { get; set; } = 0;
    public decimal TotalHoursWorked { get; set; } = 0;
    public decimal? AttendanceRate { get; set; }
    public decimal? PunctualityRate { get; set; }
    public int NoShowCount { get; set; } = 0;
    public int ViolationCount { get; set; } = 0;
    public int CommendationCount { get; set; } = 0;
    public string CurrentAvailability { get; set; } = "AVAILABLE";
    public string? AvailabilityNotes { get; set; }
    public bool BiometricRegistered { get; set; } = false;
    public string? FaceTemplateUrl { get; set; }
    public DateTime? LastAppLogin { get; set; }
    public string? DeviceTokens { get; set; }
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
    public virtual Managers? DirectManager { get; set; }


    [Write(false)]
    [Computed]
    public virtual ICollection<TeamMembers> TeamMemberships { get; set; } = new List<TeamMembers>();


    [Write(false)]
    [Computed]
    public virtual ICollection<ShiftAssignments> ShiftAssignments { get; set; } = new List<ShiftAssignments>();
}
