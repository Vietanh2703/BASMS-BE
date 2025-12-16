using Dapper;

namespace Shifts.API.GuardsHandler.GuardViewShiftAssigned;

/// <summary>
/// Query để Guard xem lịch ca trực đã được phân công
/// </summary>
public record GuardViewShiftAssignedQuery(
    Guid GuardId,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Status = null  // Filter theo status của assignment (ASSIGNED, CONFIRMED, DECLINED, etc.)
) : IQuery<GuardViewShiftAssignedResult>;

/// <summary>
/// Result chứa danh sách ca trực được phân công cho guard
/// </summary>
public record GuardViewShiftAssignedResult
{
    public bool Success { get; init; }
    public List<GuardShiftDto> Shifts { get; init; } = new();
    public int TotalCount { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO thông tin shift được phân công cho guard
/// Bao gồm thông tin shift + trạng thái assignment
/// </summary>
public record GuardShiftDto
{
    // Shift Basic Info
    public Guid ShiftId { get; init; }
    public DateTime ShiftDate { get; init; }
    public DateTime ShiftStart { get; init; }
    public DateTime ShiftEnd { get; init; }

    // Duration
    public int TotalDurationMinutes { get; init; }
    public decimal WorkDurationHours { get; init; }
    public int BreakDurationMinutes { get; init; }

    // Location
    public Guid LocationId { get; init; }
    public string? LocationName { get; init; }
    public string? LocationAddress { get; init; }
    public decimal? LocationLatitude { get; init; }
    public decimal? LocationLongitude { get; init; }

    // Shift Type & Classification
    public string ShiftType { get; init; } = string.Empty;
    public bool IsNightShift { get; init; }
    public decimal NightHours { get; init; }
    public decimal DayHours { get; init; }
    public bool IsPublicHoliday { get; init; }
    public bool IsTetHoliday { get; init; }
    public bool IsSaturday { get; init; }
    public bool IsSunday { get; init; }

    // Shift Status
    public string ShiftStatus { get; init; } = string.Empty;

    // Assignment Info
    public Guid AssignmentId { get; init; }
    public string AssignmentStatus { get; init; } = string.Empty;
    public string AssignmentType { get; init; } = string.Empty;
    public DateTime AssignedAt { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public DateTime? CheckedInAt { get; init; }
    public DateTime? CheckedOutAt { get; init; }

    // Team Info (nếu assign qua team)
    public Guid? TeamId { get; init; }
    public string? TeamName { get; init; }

    // Special Instructions
    public string? Description { get; init; }
    public string? SpecialInstructions { get; init; }
    public string? EquipmentNeeded { get; init; }
    public string? SiteAccessInfo { get; init; }

    // Flags
    public bool IsMandatory { get; init; }
    public bool IsCritical { get; init; }
    public bool RequiresArmedGuard { get; init; }
}

/// <summary>
/// Handler để Guard xem lịch ca trực được phân công
/// </summary>
internal class GuardViewShiftAssignedHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GuardViewShiftAssignedHandler> logger)
    : IQueryHandler<GuardViewShiftAssignedQuery, GuardViewShiftAssignedResult>
{
    public async Task<GuardViewShiftAssignedResult> Handle(
        GuardViewShiftAssignedQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Guard {GuardId} viewing assigned shift schedule: FromDate={FromDate}, ToDate={ToDate}, Status={Status}",
                request.GuardId,
                request.FromDate?.ToString("yyyy-MM-dd") ?? "ALL",
                request.ToDate?.ToString("yyyy-MM-dd") ?? "ALL",
                request.Status ?? "ALL");

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: VALIDATE GUARD EXISTS
            // ================================================================
            var guardExists = await connection.ExecuteScalarAsync<bool>(
                @"SELECT COUNT(1) FROM guards
                  WHERE Id = @GuardId AND IsDeleted = 0",
                new { request.GuardId });

            if (!guardExists)
            {
                logger.LogWarning("Guard {GuardId} not found", request.GuardId);
                return new GuardViewShiftAssignedResult
                {
                    Success = false,
                    ErrorMessage = $"Bảo vệ với ID {request.GuardId} không tìm thấy"
                };
            }

            // ================================================================
            // BƯỚC 2: BUILD DYNAMIC SQL QUERY
            // ================================================================
            var whereClauses = new List<string>
            {
                "sa.GuardId = @GuardId",
                "sa.IsDeleted = 0",
                "s.IsDeleted = 0",
                "s.Status != 'CANCELLED'"
            };
            var parameters = new DynamicParameters();
            parameters.Add("GuardId", request.GuardId);

            if (request.FromDate.HasValue)
            {
                whereClauses.Add("s.ShiftDate >= @FromDate");
                parameters.Add("FromDate", request.FromDate.Value.Date);
            }

            if (request.ToDate.HasValue)
            {
                whereClauses.Add("s.ShiftDate <= @ToDate");
                parameters.Add("ToDate", request.ToDate.Value.Date);
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                whereClauses.Add("sa.Status = @Status");
                parameters.Add("Status", request.Status.ToUpper());
            }

            var whereClause = string.Join(" AND ", whereClauses);

            // ================================================================
            // BƯỚC 3: COUNT TOTAL
            // ================================================================
            var countSql = $@"
                SELECT COUNT(*)
                FROM shift_assignments sa
                INNER JOIN shifts s ON sa.ShiftId = s.Id
                WHERE {whereClause}";

            var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

            logger.LogInformation("Found {TotalCount} shifts assigned to guard", totalCount);

            // ================================================================
            // BƯỚC 4: GET SHIFT SCHEDULE
            // ================================================================
            var sql = $@"
                SELECT
                    -- Shift Basic Info
                    s.Id AS ShiftId,
                    s.ShiftDate,
                    s.ShiftStart,
                    s.ShiftEnd,

                    -- Duration
                    s.TotalDurationMinutes,
                    s.WorkDurationHours,
                    s.BreakDurationMinutes,

                    -- Location
                    s.LocationId,
                    s.LocationName,
                    s.LocationAddress,
                    s.LocationLatitude,
                    s.LocationLongitude,

                    -- Shift Type & Classification
                    s.ShiftType,
                    s.IsNightShift,
                    s.NightHours,
                    s.DayHours,
                    s.IsPublicHoliday,
                    s.IsTetHoliday,
                    s.IsSaturday,
                    s.IsSunday,

                    -- Shift Status
                    s.Status AS ShiftStatus,

                    -- Assignment Info
                    sa.Id AS AssignmentId,
                    sa.Status AS AssignmentStatus,
                    sa.AssignmentType,
                    sa.AssignedAt,
                    sa.ConfirmedAt,
                    sa.CheckedInAt,
                    sa.CheckedOutAt,

                    -- Team Info
                    sa.TeamId,
                    t.TeamName,

                    -- Special Instructions
                    s.Description,
                    s.SpecialInstructions,
                    s.EquipmentNeeded,
                    s.SiteAccessInfo,

                    -- Flags
                    s.IsMandatory,
                    s.IsCritical,
                    s.RequiresArmedGuard

                FROM shift_assignments sa
                INNER JOIN shifts s ON sa.ShiftId = s.Id
                LEFT JOIN teams t ON sa.TeamId = t.Id
                WHERE {whereClause}
                ORDER BY
                    s.ShiftDate ASC,
                    s.ShiftStart ASC";

            var shifts = await connection.QueryAsync<GuardShiftDto>(sql, parameters);
            var shiftsList = shifts.ToList();

            logger.LogInformation(
                "✓ Retrieved {Count} shifts for guard {GuardId} - sorted by date and time",
                shiftsList.Count,
                request.GuardId);

            return new GuardViewShiftAssignedResult
            {
                Success = true,
                Shifts = shiftsList,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting assigned shifts for guard {GuardId}",
                request.GuardId);

            return new GuardViewShiftAssignedResult
            {
                Success = false,
                Shifts = new List<GuardShiftDto>(),
                TotalCount = 0,
                ErrorMessage = $"Lỗi khi lấy lịch ca trực: {ex.Message}"
            };
        }
    }
}
