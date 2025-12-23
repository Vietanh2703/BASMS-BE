namespace Shifts.API.ShiftsHandler.BulkCancelShift;

public record BulkCancelShiftRequest
{
    public Guid GuardId { get; init; }
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public string CancellationReason { get; init; } = string.Empty;
    public string LeaveType { get; init; } = "OTHER";
    public Guid CancelledBy { get; init; }
}
