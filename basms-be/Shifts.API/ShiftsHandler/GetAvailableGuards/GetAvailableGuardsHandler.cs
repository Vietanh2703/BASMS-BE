namespace Shifts.API.ShiftsHandler.GetAvailableGuards;


public record GetAvailableGuardsQuery(
    Guid LocationId,
    DateTime ShiftDate,
    TimeSpan StartTime,
    TimeSpan EndTime
) : IQuery<GetAvailableGuardsResult>;


public record GetAvailableGuardsResult(
    List<GuardAvailability> Guards,
    int TotalGuards,
    int AvailableCount,
    int BusyCount,
    int OnLeaveCount
);


public record GuardAvailability(
    Guid GuardId,
    string EmployeeCode,
    string FullName,
    string Email,
    string PhoneNumber,
    GuardStatus Status,
    string? Reason,            
    ConflictingShift? ConflictShift  
);

public enum GuardStatus
{
    Available,      
    Busy,           
    OnLeave         
}

public record ConflictingShift(
    Guid ShiftId,
    DateTime ShiftDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string LocationName
);

internal class GetAvailableGuardsHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetAvailableGuardsHandler> logger)
    : IQueryHandler<GetAvailableGuardsQuery, GetAvailableGuardsResult>
{
    public async Task<GetAvailableGuardsResult> Handle(
        GetAvailableGuardsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting available guards for location {LocationId} on {ShiftDate:yyyy-MM-dd} {StartTime}-{EndTime}",
                request.LocationId,
                request.ShiftDate,
                request.StartTime,
                request.EndTime);

            using var connection = await dbFactory.CreateConnectionAsync();

            var shiftStart = request.ShiftDate.Date.Add(request.StartTime);
            var shiftEnd = request.ShiftDate.Date.Add(request.EndTime);
            
            var allGuardsSql = @"
                SELECT
                    Id AS GuardId,
                    EmployeeCode,
                    FullName,
                    Email,
                    PhoneNumber
                FROM guards
                WHERE IsDeleted = 0
                    AND EmploymentStatus = 'ACTIVE'
                ORDER BY FullName";

            var allGuards = await connection.QueryAsync<GuardInfo>(allGuardsSql);
            var allGuardsList = allGuards.ToList();

            logger.LogInformation("Found {Count} total active guards", allGuardsList.Count);

            var busyGuardsSql = @"
                SELECT
                    sa.GuardId,
                    s.Id AS ShiftId,
                    s.ShiftDate,
                    s.ShiftStart,
                    s.ShiftEnd,
                    s.LocationId,
                    'Location' AS LocationName
                FROM shift_assignments sa
                INNER JOIN shifts s ON sa.ShiftId = s.Id
                WHERE sa.IsDeleted = 0
                    AND s.IsDeleted = 0
                    AND sa.Status NOT IN ('CANCELLED', 'DECLINED')
                    AND s.ShiftDate = @ShiftDate
                    AND (s.ShiftStart < @ShiftEnd AND s.ShiftEnd > @ShiftStart)";

            var busyGuards = await connection.QueryAsync<BusyGuardInfo>(
                busyGuardsSql,
                new
                {
                    ShiftDate = request.ShiftDate.Date,
                    ShiftStart = shiftStart,
                    ShiftEnd = shiftEnd
                });

            var busyGuardsDict = busyGuards
                .GroupBy(x => x.GuardId)
                .ToDictionary(x => x.Key, x => x.First());

            logger.LogInformation("Found {Count} busy guards", busyGuardsDict.Count);
            
            var onLeaveGuards = new Dictionary<Guid, string>();
            
            var guardAvailabilities = new List<GuardAvailability>();

            foreach (var guard in allGuardsList)
            {
                GuardStatus status;
                string? reason = null;
                ConflictingShift? conflictShift = null;

                if (onLeaveGuards.ContainsKey(guard.GuardId))
                {
                    status = GuardStatus.OnLeave;
                    reason = onLeaveGuards[guard.GuardId];
                }
                else if (busyGuardsDict.TryGetValue(guard.GuardId, out var busyInfo))
                {
                    status = GuardStatus.Busy;
                    reason = $"Đã có ca trực khác ({busyInfo.ShiftStart.TimeOfDay:hh\\:mm} - {busyInfo.ShiftEnd.TimeOfDay:hh\\:mm})";
                    conflictShift = new ConflictingShift(
                        busyInfo.ShiftId,
                        busyInfo.ShiftDate,
                        busyInfo.ShiftStart.TimeOfDay,
                        busyInfo.ShiftEnd.TimeOfDay,
                        busyInfo.LocationName
                    );
                }
                else
                {
                    status = GuardStatus.Available;
                }

                guardAvailabilities.Add(new GuardAvailability(
                    guard.GuardId,
                    guard.EmployeeCode,
                    guard.FullName,
                    guard.Email ?? "",
                    guard.PhoneNumber ?? "",
                    status,
                    reason,
                    conflictShift
                ));
            }

            var availableCount = guardAvailabilities.Count(x => x.Status == GuardStatus.Available);
            var busyCount = guardAvailabilities.Count(x => x.Status == GuardStatus.Busy);
            var onLeaveCount = guardAvailabilities.Count(x => x.Status == GuardStatus.OnLeave);

            logger.LogInformation(
                "Guard availability: {Available} available, {Busy} busy, {OnLeave} on leave",
                availableCount,
                busyCount,
                onLeaveCount);

            return new GetAvailableGuardsResult(
                guardAvailabilities,
                allGuardsList.Count,
                availableCount,
                busyCount,
                onLeaveCount
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting available guards");
            throw;
        }
    }
}


internal class GuardInfo
{
    public Guid GuardId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}


internal class BusyGuardInfo
{
    public Guid GuardId { get; set; }
    public Guid ShiftId { get; set; }
    public DateTime ShiftDate { get; set; }
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
}
