namespace Shifts.API.ShiftsHandler.GetGuardGroupByShiftId;

public record GetGuardGroupByShiftIdQuery(Guid ShiftId) : IQuery<GetGuardGroupByShiftIdResult>;

public record GetGuardGroupByShiftIdResult
{
    public bool Success { get; init; }
    public Guid ShiftId { get; init; }
    public Guid? TeamId { get; init; }
    public string? TeamName { get; init; }
    public List<GuardInShiftDto> Guards { get; init; } = new();
    public int TotalGuards { get; init; }
    public string? ErrorMessage { get; init; }
}

public record GuardInShiftDto
{
    public Guid GuardId { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string? Email { get; init; }
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Gender { get; init; }
    public string EmploymentStatus { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty; 
    public bool IsLeader { get; init; }
    public Guid AssignmentId { get; init; }
    public string AssignmentStatus { get; init; } = string.Empty;
    public string AssignmentType { get; init; } = string.Empty;
    public DateTime AssignedAt { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public string? CertificationLevel { get; init; }
}


internal class GetGuardGroupByShiftIdHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetGuardGroupByShiftIdHandler> logger)
    : IQueryHandler<GetGuardGroupByShiftIdQuery, GetGuardGroupByShiftIdResult>
{
    public async Task<GetGuardGroupByShiftIdResult> Handle(
        GetGuardGroupByShiftIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting guard group for Shift {ShiftId}",
                request.ShiftId);

            using var connection = await dbFactory.CreateConnectionAsync();
            var shiftExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) > 0 FROM shifts WHERE Id = @ShiftId AND IsDeleted = 0",
                new { ShiftId = request.ShiftId });

            if (!shiftExists)
            {
                logger.LogWarning("Shift {ShiftId} not found", request.ShiftId);
                return new GetGuardGroupByShiftIdResult
                {
                    Success = false,
                    ShiftId = request.ShiftId,
                    ErrorMessage = $"Shift {request.ShiftId} not found"
                };
            }

            var sql = @"
                SELECT
                    sa.Id AS AssignmentId,
                    sa.ShiftId,
                    sa.TeamId,
                    sa.GuardId,
                    sa.Status AS AssignmentStatus,
                    sa.AssignmentType,
                    sa.AssignedAt,
                    sa.ConfirmedAt,
                    g.EmployeeCode,
                    g.FullName,
                    g.AvatarUrl,
                    g.Email,
                    g.PhoneNumber,
                    g.Gender,
                    g.EmploymentStatus,
                    g.CertificationLevel,
                    COALESCE(tm.Role, 'MEMBER') AS Role,
                    t.TeamName

                FROM shift_assignments sa
                INNER JOIN guards g ON sa.GuardId = g.Id AND g.IsDeleted = 0
                LEFT JOIN team_members tm ON sa.TeamId = tm.TeamId AND sa.GuardId = tm.GuardId AND tm.IsDeleted = 0
                LEFT JOIN teams t ON sa.TeamId = t.Id AND t.IsDeleted = 0

                WHERE
                    sa.ShiftId = @ShiftId
                    AND sa.IsDeleted = 0

                ORDER BY
                    CASE tm.Role
                        WHEN 'LEADER' THEN 1
                        WHEN 'DEPUTY' THEN 2
                        WHEN 'MEMBER' THEN 3
                        ELSE 4
                    END,
                    g.FullName ASC";

            var results = await connection.QueryAsync(sql, new { ShiftId = request.ShiftId });
            var resultsList = results.ToList();

            if (!resultsList.Any())
            {
                logger.LogInformation("No guards assigned to Shift {ShiftId}", request.ShiftId);
                return new GetGuardGroupByShiftIdResult
                {
                    Success = true,
                    ShiftId = request.ShiftId,
                    Guards = new List<GuardInShiftDto>(),
                    TotalGuards = 0
                };
            }
            
            var guards = resultsList.Select(r => new GuardInShiftDto
            {
                AssignmentId = r.AssignmentId,
                GuardId = r.GuardId,
                EmployeeCode = r.EmployeeCode ?? string.Empty,
                FullName = r.FullName ?? string.Empty,
                AvatarUrl = r.AvatarUrl,
                Email = r.Email,
                PhoneNumber = r.PhoneNumber ?? string.Empty,
                Gender = r.Gender,
                EmploymentStatus = r.EmploymentStatus ?? string.Empty,
                Role = r.Role ?? "MEMBER",
                IsLeader = (r.Role ?? "MEMBER") == "LEADER",
                AssignmentStatus = r.AssignmentStatus ?? string.Empty,
                AssignmentType = r.AssignmentType ?? string.Empty,
                AssignedAt = r.AssignedAt,
                ConfirmedAt = r.ConfirmedAt,
                CertificationLevel = r.CertificationLevel
            }).ToList();

            var firstResult = resultsList.First();
            var teamId = firstResult.TeamId as Guid?;
            var teamName = firstResult.TeamName as string;

            logger.LogInformation(
                "Found {Count} guards for Shift {ShiftId} (Team: {TeamName})",
                guards.Count,
                request.ShiftId,
                teamName ?? "Individual Assignment");

            return new GetGuardGroupByShiftIdResult
            {
                Success = true,
                ShiftId = request.ShiftId,
                TeamId = teamId,
                TeamName = teamName,
                Guards = guards,
                TotalGuards = guards.Count
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting guard group for Shift {ShiftId}",
                request.ShiftId);

            return new GetGuardGroupByShiftIdResult
            {
                Success = false,
                ShiftId = request.ShiftId,
                ErrorMessage = $"Failed to get guard group: {ex.Message}"
            };
        }
    }
}
