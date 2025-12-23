namespace Shifts.API.ShiftsHandler.GetUnassignedShiftGroups;

public record GetUnassignedShiftGroupsQuery(
    Guid ManagerId,
    Guid? ContractId = null
) : IQuery<GetUnassignedShiftGroupsResult>;


public record GetUnassignedShiftGroupsResult
{
    public bool Success { get; init; }
    public List<UnassignedShiftGroupDto> ShiftGroups { get; init; } = new();
    public int TotalGroups { get; init; }
    public string? ErrorMessage { get; init; }
}

public record UnassignedShiftGroupDto
{
    public Guid RepresentativeShiftId { get; init; }
    public Guid? ShiftTemplateId { get; init; }
    public Guid? ContractId { get; init; }
    public string? TemplateName { get; init; }
    public string? TemplateCode { get; init; }
    public string? ContractNumber { get; init; }
    public string? ContractTitle { get; init; }
    public Guid? LocationId { get; init; }
    public string? LocationName { get; init; }
    public string? LocationAddress { get; init; }
    public DateTime? ShiftStart { get; init; }
    public DateTime? ShiftEnd { get; init; }
    public decimal? WorkDurationHours { get; init; }
    public int UnassignedShiftCount { get; init; }
    public int RequiredGuards { get; init; }
    public DateTime? NearestShiftDate { get; init; }
    public DateTime? FarthestShiftDate { get; init; }
    public bool IsNightShift { get; init; }
    public string? ShiftType { get; init; }
}
