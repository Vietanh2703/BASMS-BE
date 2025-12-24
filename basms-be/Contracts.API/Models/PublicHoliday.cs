namespace Contracts.API.Models;

[Table("public_holidays")]
public class PublicHoliday
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid? ContractId { get; set; }
    public DateTime HolidayDate { get; set; }
    public string HolidayName { get; set; } = string.Empty;
    public string? HolidayNameEn { get; set; }
    public string HolidayCategory { get; set; } = string.Empty;
    public bool IsTetPeriod { get; set; } = false;
    public bool IsTetHoliday { get; set; } = false;
    public int? TetDayNumber { get; set; }
    public DateTime? HolidayStartDate { get; set; }
    public DateTime? HolidayEndDate { get; set; }
    public int? TotalHolidayDays { get; set; }
    public bool IsOfficialHoliday { get; set; } = true;
    public bool IsObserved { get; set; } = true;
    public DateTime? OriginalDate { get; set; }
    public DateTime? ObservedDate { get; set; }
    public bool AppliesNationwide { get; set; } = true;
    public string? AppliesToRegions { get; set; }
    public bool StandardWorkplacesClosed { get; set; } = true;
    public bool EssentialServicesOperating { get; set; } = true;
    public string? Description { get; set; }
    public int Year { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
