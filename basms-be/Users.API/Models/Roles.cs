namespace Users.API.Models;

[Table("roles")]
public class Roles
{
    [ExplicitKey]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Permissions { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }
    
    [Write(false)]
    [Computed]
    public virtual ICollection<Users> Users { get; set; } = new List<Users>();
}