namespace Shifts.API.Models;

[Table("team_members")]
public class TeamMembers
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid GuardId { get; set; }
    public string Role { get; set; } = "MEMBER";
    public bool IsActive { get; set; } = true;
    public decimal? PerformanceRating { get; set; }
    public int TotalShiftsCompleted { get; set; } = 0;
    public int TotalShiftsAssigned { get; set; } = 0;
    public decimal? AttendanceRate { get; set; }
    public string? JoiningNotes { get; set; }
    public string? LeavingNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    [Write(false)]
    [Computed]
    public virtual Teams? Team { get; set; }

  
    [Write(false)]
    [Computed]
    public virtual Guards? Guard { get; set; }
}