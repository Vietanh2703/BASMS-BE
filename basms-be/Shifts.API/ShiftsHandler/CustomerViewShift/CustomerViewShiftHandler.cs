using Dapper;

namespace Shifts.API.ShiftsHandler.CustomerViewShift;

/// <summary>
/// Query để Customer xem các ca trực của contract
/// </summary>
public record CustomerViewShiftQuery(
    Guid ContractId,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Status = null,      // DRAFT, SCHEDULED, IN_PROGRESS, COMPLETED, CANCELLED
    string? ShiftType = null    // REGULAR, OVERTIME, EMERGENCY, REPLACEMENT, TRAINING
) : IQuery<CustomerViewShiftResult>;

/// <summary>
/// Result chứa danh sách ca trực của contract
/// </summary>
public record CustomerViewShiftResult
{
    public bool Success { get; init; }
    public List<CustomerShiftDto> Shifts { get; init; } = new();
    public int TotalCount { get; init; }
    public ContractShiftSummary? Summary { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO thông tin ca trực cho Customer xem
/// </summary>
public record CustomerShiftDto
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

    // Staffing Info
    public int RequiredGuards { get; init; }
    public int AssignedGuardsCount { get; init; }
    public int ConfirmedGuardsCount { get; init; }
    public int CheckedInGuardsCount { get; init; }
    public int CompletedGuardsCount { get; init; }
    public bool IsFullyStaffed { get; init; }
    public bool IsUnderstaffed { get; init; }
    public decimal? StaffingPercentage { get; init; }

    // Status
    public string Status { get; init; } = string.Empty;

    // Flags
    public bool IsMandatory { get; init; }
    public bool IsCritical { get; init; }
    public bool RequiresArmedGuard { get; init; }

    // Description
    public string? Description { get; init; }
    public string? SpecialInstructions { get; init; }
}

/// <summary>
/// Tóm tắt thông tin ca trực của contract
/// </summary>
public record ContractShiftSummary
{
    public int TotalShifts { get; init; }
    public int ScheduledShifts { get; init; }
    public int InProgressShifts { get; init; }
    public int CompletedShifts { get; init; }
    public int CancelledShifts { get; init; }

    public int FullyStaffedShifts { get; init; }
    public int UnderstaffedShifts { get; init; }

    public decimal TotalHoursScheduled { get; init; }
    public decimal TotalHoursCompleted { get; init; }

    public int NightShifts { get; init; }
    public int WeekendShifts { get; init; }
    public int HolidayShifts { get; init; }
}

/// <summary>
/// Handler để Customer xem các ca trực của contract
/// </summary>
internal class CustomerViewShiftHandler(
    IDbConnectionFactory dbFactory,
    ILogger<CustomerViewShiftHandler> logger)
    : IQueryHandler<CustomerViewShiftQuery, CustomerViewShiftResult>
{
    public async Task<CustomerViewShiftResult> Handle(
        CustomerViewShiftQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Customer viewing shifts for contract {ContractId}: FromDate={FromDate}, ToDate={ToDate}, Status={Status}",
                request.ContractId,
                request.FromDate?.ToString("yyyy-MM-dd") ?? "ALL",
                request.ToDate?.ToString("yyyy-MM-dd") ?? "ALL",
                request.Status ?? "ALL");

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: BUILD DYNAMIC SQL QUERY
            // ================================================================
            var whereClauses = new List<string>
            {
                "ContractId = @ContractId",
                "IsDeleted = 0",
                "Status != 'CANCELLED'"
            };
            var parameters = new DynamicParameters();
            parameters.Add("ContractId", request.ContractId);

            if (request.FromDate.HasValue)
            {
                whereClauses.Add("ShiftDate >= @FromDate");
                parameters.Add("FromDate", request.FromDate.Value.Date);
            }

            if (request.ToDate.HasValue)
            {
                whereClauses.Add("ShiftDate <= @ToDate");
                parameters.Add("ToDate", request.ToDate.Value.Date);
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                whereClauses.Add("Status = @Status");
                parameters.Add("Status", request.Status.ToUpper());
            }

            if (!string.IsNullOrWhiteSpace(request.ShiftType))
            {
                whereClauses.Add("ShiftType = @ShiftType");
                parameters.Add("ShiftType", request.ShiftType.ToUpper());
            }

            var whereClause = string.Join(" AND ", whereClauses);

            // ================================================================
            // BƯỚC 2: COUNT TOTAL
            // ================================================================
            var countSql = $@"
                SELECT COUNT(*)
                FROM shifts
                WHERE {whereClause}";

            var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

            logger.LogInformation("Found {TotalCount} shifts for contract", totalCount);

            // ================================================================
            // BƯỚC 3: GET SHIFTS
            // ================================================================
            var sql = $@"
                SELECT
                    -- Shift Basic Info
                    Id AS ShiftId,
                    ShiftDate,
                    ShiftStart,
                    ShiftEnd,

                    -- Duration
                    TotalDurationMinutes,
                    WorkDurationHours,
                    BreakDurationMinutes,

                    -- Location
                    LocationId,
                    LocationName,
                    LocationAddress,
                    LocationLatitude,
                    LocationLongitude,

                    -- Shift Type & Classification
                    ShiftType,
                    IsNightShift,
                    NightHours,
                    DayHours,
                    IsPublicHoliday,
                    IsTetHoliday,
                    IsSaturday,
                    IsSunday,

                    -- Staffing Info
                    RequiredGuards,
                    AssignedGuardsCount,
                    ConfirmedGuardsCount,
                    CheckedInGuardsCount,
                    CompletedGuardsCount,
                    IsFullyStaffed,
                    IsUnderstaffed,
                    StaffingPercentage,

                    -- Status
                    Status,

                    -- Flags
                    IsMandatory,
                    IsCritical,
                    RequiresArmedGuard,

                    -- Description
                    Description,
                    SpecialInstructions

                FROM shifts
                WHERE {whereClause}
                ORDER BY
                    ShiftDate ASC,
                    ShiftStart ASC";

            var shifts = await connection.QueryAsync<CustomerShiftDto>(sql, parameters);
            var shiftsList = shifts.ToList();

            logger.LogInformation(
                "✓ Retrieved {Count} shifts for contract {ContractId} - sorted by date and time",
                shiftsList.Count,
                request.ContractId);

            // ================================================================
            // BƯỚC 4: CALCULATE SUMMARY
            // ================================================================
            var summary = CalculateSummary(shiftsList);

            return new CustomerViewShiftResult
            {
                Success = true,
                Shifts = shiftsList,
                TotalCount = totalCount,
                Summary = summary
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting shifts for contract {ContractId}",
                request.ContractId);

            return new CustomerViewShiftResult
            {
                Success = false,
                Shifts = new List<CustomerShiftDto>(),
                TotalCount = 0,
                ErrorMessage = $"Lỗi khi lấy danh sách ca trực: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Tính toán tóm tắt thông tin ca trực
    /// </summary>
    private ContractShiftSummary CalculateSummary(List<CustomerShiftDto> shifts)
    {
        return new ContractShiftSummary
        {
            TotalShifts = shifts.Count,
            ScheduledShifts = shifts.Count(s => s.Status == "SCHEDULED"),
            InProgressShifts = shifts.Count(s => s.Status == "IN_PROGRESS"),
            CompletedShifts = shifts.Count(s => s.Status == "COMPLETED"),
            CancelledShifts = shifts.Count(s => s.Status == "CANCELLED"),

            FullyStaffedShifts = shifts.Count(s => s.IsFullyStaffed),
            UnderstaffedShifts = shifts.Count(s => s.IsUnderstaffed),

            TotalHoursScheduled = shifts
                .Where(s => s.Status != "CANCELLED")
                .Sum(s => s.WorkDurationHours * s.RequiredGuards),
            TotalHoursCompleted = shifts
                .Where(s => s.Status == "COMPLETED")
                .Sum(s => s.WorkDurationHours * s.CompletedGuardsCount),

            NightShifts = shifts.Count(s => s.IsNightShift),
            WeekendShifts = shifts.Count(s => s.IsSaturday || s.IsSunday),
            HolidayShifts = shifts.Count(s => s.IsPublicHoliday || s.IsTetHoliday)
        };
    }
}
