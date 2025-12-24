namespace Shifts.API.Models;

[Table("teams")]
public class Teams
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid ManagerId { get; set; }
    public string TeamCode { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MinMembers { get; set; } = 1;
    public int? MaxMembers { get; set; }
    public int CurrentMemberCount { get; set; } = 0;
    public string? Specialization { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    
    [Write(false)]
    [Computed]
    public virtual Managers? Manager { get; set; }
    
    [Write(false)]
    [Computed]
    public virtual ICollection<TeamMembers> Members { get; set; } = new List<TeamMembers>();


    [Write(false)]
    [Computed]
    public virtual ICollection<ShiftAssignments> ShiftAssignments { get; set; } = new List<ShiftAssignments>();
}