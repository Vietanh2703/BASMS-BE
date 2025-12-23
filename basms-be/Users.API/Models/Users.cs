namespace Users.API.Models;

[Table("users")]
public class Users
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public string FirebaseUid { get; set; } = string.Empty;
    public string? IdentityNumber { get; set; }
    public DateTime IdentityIssueDate { get; set; }
    public string IdentityIssuePlace { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int? BirthDay { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthYear { get; set; }
    public Guid RoleId { get; set; }
    public string AuthProvider { get; set; } = "email";
    public string Status { get; set; } = "active";
    public DateTime? LastLoginAt { get; set; }
    public int LoginCount { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsActive { get; set; } = true;
    public string Password { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    public Guid? CreatedBy { get; set; }
    
    public Guid? UpdatedBy { get; set; }
    
    [Write(false)]
    [Computed]
    public virtual Roles? Role { get; set; }
    
    [Write(false)]
    [Computed]
    public virtual ICollection<UserTokens> UserTokens { get; set; } = new List<UserTokens>();
    
    [Write(false)]
    [Computed]
    public virtual ICollection<RefreshTokens> RefreshTokens { get; set; } = new List<RefreshTokens>();
    
    [Write(false)]
    [Computed]
    public virtual ICollection<AuditLogs> AuditLogs { get; set; } = new List<AuditLogs>();
    
    [Write(false)]
    [Computed]
    public virtual ICollection<UserSessions> UserSessions { get; set; } = new List<UserSessions>();
    
    [Write(false)]
    [Computed]
    public virtual ICollection<OTPLogs> OTPLogs { get; set; } = new List<OTPLogs>();
}