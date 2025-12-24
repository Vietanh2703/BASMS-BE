namespace Contracts.API.Models;

[Table("customer_locations")]
public class CustomerLocation
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string LocationType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int GeofenceRadiusMeters { get; set; } = 100;
    public decimal? AltitudeMeters { get; set; }
    public string? SiteManagerName { get; set; }
    public string? SiteManagerPhone { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string OperatingHoursType { get; set; } = "24/7";
    public decimal? TotalAreaSqm { get; set; }
    public int? BuildingFloors { get; set; }
    public bool FollowsStandardWorkweek { get; set; } = true;
    public string? CustomWeekendDays { get; set; }
    public bool Requires24x7Coverage { get; set; } = false;
    public bool AllowsSingleGuard { get; set; } = true;
    public int MinimumGuardsRequired { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
