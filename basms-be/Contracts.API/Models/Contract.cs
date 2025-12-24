namespace Contracts.API.Models;


[Table("contracts")]
public class Contract
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? DocumentId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string ContractTitle { get; set; } = string.Empty;
    public string ContractType { get; set; } = string.Empty;
    public string ServiceScope { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DurationMonths { get; set; }
    public bool IsRenewable { get; set; } = true;
    public bool AutoRenewal { get; set; } = false;
    public int RenewalNoticeDays { get; set; } = 30;
    public int RenewalCount { get; set; } = 0;
    public string CoverageModel { get; set; } = string.Empty;
    public bool FollowsCustomerCalendar { get; set; } = true;
    public bool WorkOnPublicHolidays { get; set; } = true;
    public bool WorkOnCustomerClosedDays { get; set; } = true;
    public bool AutoGenerateShifts { get; set; } = true;
    public int GenerateShiftsAdvanceDays { get; set; } = 30;
    public string Status { get; set; } = "draft";
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationType { get; set; }
    public string? TerminationReason { get; set; }
    public Guid? TerminatedBy { get; set; }
    public string? ContractFileUrl { get; set; }
    public DateTime? SignedDate { get; set; }
    public string? Notes { get; set; }
    public decimal? MonthlyWage { get; set; }
    public string? MonthlyWageInWords { get; set; }
    public string? CertificationLevel { get; set; }
    public string? JobTitle { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
