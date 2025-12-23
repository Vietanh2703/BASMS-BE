namespace Shifts.API.Models;

[Table("wage_rates")]
public class WageRates
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public string CertificationLevel { get; set; } = string.Empty;
    public decimal MinWage { get; set; }
    public decimal MaxWage { get; set; }
    public decimal StandardWage { get; set; }
    public string? StandardWageInWords { get; set; }
    public string Currency { get; set; } = "VNĐ";
    public string? Description { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
