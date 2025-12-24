namespace Contracts.API.Models;


[Table("holiday_substitute_work_days")]
public class HolidaySubstituteWorkDay
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid HolidayId { get; set; }
    public DateTime SubstituteDate { get; set; }
    public string? Reason { get; set; }
    public int Year { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
