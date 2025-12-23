namespace Shifts.API.ShiftsHandler.AssignTeamToShift;

public record AssignTeamToShiftCommand(
    Guid TeamId,                  
    DateTime StartDate,           
    DateTime EndDate,              
    string ShiftTimeSlot,          
    Guid LocationId,                
    Guid? ContractId,               
    string AssignmentType,          
    string? AssignmentNotes,        
    Guid AssignedBy                 
) : ICommand<AssignTeamToShiftResult>;


public record AssignTeamToShiftResult
{
    public bool Success { get; init; }
    public int TotalDaysProcessed { get; init; }
    public int TotalShiftsAssigned { get; init; }
    public int TotalGuardsAssigned { get; init; }
    public List<DailyAssignmentSummary> DailySummaries { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public List<string> Errors { get; init; } = new();
}


public record DailyAssignmentSummary
{
    public DateTime Date { get; init; }
    public Guid ShiftId { get; init; }
    public string ShiftCode { get; init; } = string.Empty;
    public string ShiftTimeSlot { get; init; } = string.Empty;
    public DateTime ShiftStart { get; init; }
    public DateTime ShiftEnd { get; init; }
    public int GuardsAssigned { get; init; }
    public List<string> GuardNames { get; init; } = new();
    public List<string> SkippedGuards { get; init; } = new();
}
