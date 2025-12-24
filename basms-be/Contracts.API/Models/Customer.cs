namespace Contracts.API.Models;

[Table("customers")]
public class Customer
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string ContactPersonName { get; set; } = string.Empty;
    public string? ContactPersonTitle { get; set; }
    public string IdentityNumber { get; set; } = string.Empty;
    public DateTime? IdentityIssueDate { get; set; }
    public string? IdentityIssuePlace { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AvatarUrl { get; set; }
    public string? Gender { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Industry { get; set; }
    public string? CompanySize { get; set; }
    public DateTime CustomerSince { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "active";
    public bool FollowsNationalHolidays { get; set; } = true;
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
