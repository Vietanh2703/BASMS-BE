using Dapper;

namespace Shifts.API.ShiftsHandler.GetAllShifts;

/// <summary>
/// Query để lấy danh sách tất cả shifts với filtering
/// </summary>
public record GetAllShiftsQuery(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    Guid? ManagerId = null,
    Guid? LocationId = null,
    Guid? ContractId = null,
    string? Status = null,
    string? ShiftType = null,
    bool? IsNightShift = null,
    int PageNumber = 1,
    int PageSize = 50
) : IQuery<GetAllShiftsResult>;

/// <summary>
/// Result chứa danh sách shifts
/// </summary>
public record GetAllShiftsResult
{
    public bool Success { get; init; }
    public List<ShiftDto> Shifts { get; init; } = new();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO cho shift
/// </summary>
public record ShiftDto
{
    public Guid Id { get; init; }

    // Foreign Keys
    public Guid? ShiftTemplateId { get; init; }
    public Guid LocationId { get; init; }
    public string? LocationName { get; init; }
    public string? LocationAddress { get; init; }
    public decimal? LocationLatitude { get; init; }
    public decimal? LocationLongitude { get; init; }
    public Guid? ContractId { get; init; }
    public Guid? ManagerId { get; init; }

    // Date Info
    public DateTime ShiftDate { get; init; }
    public int ShiftDay { get; init; }
    public int ShiftMonth { get; init; }
    public int ShiftYear { get; init; }
    public int ShiftQuarter { get; init; }
    public int ShiftWeek { get; init; }
    public int DayOfWeek { get; init; }
    public DateTime? ShiftEndDate { get; init; }

    // Time
    public DateTime ShiftStart { get; init; }
    public DateTime ShiftEnd { get; init; }

    // Duration
    public int TotalDurationMinutes { get; init; }
    public int WorkDurationMinutes { get; init; }
    public decimal WorkDurationHours { get; init; }
    public int BreakDurationMinutes { get; init; }
    public int PaidBreakMinutes { get; init; }
    public int UnpaidBreakMinutes { get; init; }

    // Staffing
    public int RequiredGuards { get; init; }
    public int AssignedGuardsCount { get; init; }
    public int ConfirmedGuardsCount { get; init; }
    public int CheckedInGuardsCount { get; init; }
    public int CompletedGuardsCount { get; init; }
    public bool IsFullyStaffed { get; init; }
    public bool IsUnderstaffed { get; init; }
    public bool IsOverstaffed { get; init; }
    public decimal? StaffingPercentage { get; init; }

    // Classification
    public bool IsRegularWeekday { get; init; }
    public bool IsSaturday { get; init; }
    public bool IsSunday { get; init; }
    public bool IsPublicHoliday { get; init; }
    public bool IsTetHoliday { get; init; }
    public bool IsNightShift { get; init; }
    public decimal NightHours { get; init; }
    public decimal DayHours { get; init; }
    public string ShiftType { get; init; } = string.Empty;

    // Flags
    public bool IsMandatory { get; init; }
    public bool IsCritical { get; init; }
    public bool IsTrainingShift { get; init; }
    public bool RequiresArmedGuard { get; init; }

    // Approval
    public bool RequiresApproval { get; init; }
    public Guid? ApprovedBy { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string ApprovalStatus { get; init; } = string.Empty;
    public string? RejectionReason { get; init; }

    // Status & Lifecycle
    public string Status { get; init; } = string.Empty;
    public DateTime? ScheduledAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }

    // Description
    public string? Description { get; init; }
    public string? SpecialInstructions { get; init; }

    // Audit
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? UpdatedBy { get; init; }
    public int Version { get; init; }
}

/// <summary>
/// Handler để lấy danh sách tất cả shifts với filtering và pagination
/// </summary>
internal class GetAllShiftsHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetAllShiftsHandler> logger)
    : IQueryHandler<GetAllShiftsQuery, GetAllShiftsResult>
{
    public async Task<GetAllShiftsResult> Handle(
        GetAllShiftsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting shifts: FromDate={FromDate}, ToDate={ToDate}, ManagerId={ManagerId}, Status={Status}, Page={Page}, PageSize={PageSize}",
                request.FromDate?.ToString("yyyy-MM-dd") ?? "ALL",
                request.ToDate?.ToString("yyyy-MM-dd") ?? "ALL",
                request.ManagerId?.ToString() ?? "ALL",
                request.Status ?? "ALL",
                request.PageNumber,
                request.PageSize);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BUILD DYNAMIC SQL QUERY
            // ================================================================
            var whereClauses = new List<string> { "IsDeleted = 0" };
            var parameters = new DynamicParameters();

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

            if (request.ManagerId.HasValue)
            {
                whereClauses.Add("ManagerId = @ManagerId");
                parameters.Add("ManagerId", request.ManagerId.Value);
            }

            if (request.LocationId.HasValue)
            {
                whereClauses.Add("LocationId = @LocationId");
                parameters.Add("LocationId", request.LocationId.Value);
            }

            if (request.ContractId.HasValue)
            {
                whereClauses.Add("ContractId = @ContractId");
                parameters.Add("ContractId", request.ContractId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                whereClauses.Add("Status = @Status");
                parameters.Add("Status", request.Status);
            }

            if (!string.IsNullOrWhiteSpace(request.ShiftType))
            {
                whereClauses.Add("ShiftType = @ShiftType");
                parameters.Add("ShiftType", request.ShiftType);
            }

            if (request.IsNightShift.HasValue)
            {
                whereClauses.Add("IsNightShift = @IsNightShift");
                parameters.Add("IsNightShift", request.IsNightShift.Value);
            }

            var whereClause = string.Join(" AND ", whereClauses);

            // ================================================================
            // COUNT TOTAL RECORDS
            // ================================================================
            var countSql = $@"
                SELECT COUNT(*)
                FROM shifts
                WHERE {whereClause}";

            var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
            var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

            logger.LogInformation(
                "Total shifts found: {TotalCount}, Total pages: {TotalPages}",
                totalCount,
                totalPages);

            // ================================================================
            // GET PAGINATED DATA
            // ================================================================
            var offset = (request.PageNumber - 1) * request.PageSize;
            parameters.Add("Offset", offset);
            parameters.Add("PageSize", request.PageSize);

            var sql = $@"
                SELECT
                    Id,
                    ShiftTemplateId,
                    LocationId,
                    LocationName,
                    LocationAddress,
                    LocationLatitude,
                    LocationLongitude,
                    ContractId,
                    ManagerId,
                    ShiftDate,
                    ShiftDay,
                    ShiftMonth,
                    ShiftYear,
                    ShiftQuarter,
                    ShiftWeek,
                    DayOfWeek,
                    ShiftEndDate,
                    ShiftStart,
                    ShiftEnd,
                    TotalDurationMinutes,
                    WorkDurationMinutes,
                    WorkDurationHours,
                    BreakDurationMinutes,
                    PaidBreakMinutes,
                    UnpaidBreakMinutes,
                    RequiredGuards,
                    AssignedGuardsCount,
                    ConfirmedGuardsCount,
                    CheckedInGuardsCount,
                    CompletedGuardsCount,
                    IsFullyStaffed,
                    IsUnderstaffed,
                    IsOverstaffed,
                    StaffingPercentage,
                    IsRegularWeekday,
                    IsSaturday,
                    IsSunday,
                    IsPublicHoliday,
                    IsTetHoliday,
                    IsNightShift,
                    NightHours,
                    DayHours,
                    ShiftType,
                    IsMandatory,
                    IsCritical,
                    IsTrainingShift,
                    RequiresArmedGuard,
                    RequiresApproval,
                    ApprovedBy,
                    ApprovedAt,
                    ApprovalStatus,
                    RejectionReason,
                    Status,
                    ScheduledAt,
                    StartedAt,
                    CompletedAt,
                    CancelledAt,
                    CancellationReason,
                    Description,
                    SpecialInstructions,
                    CreatedAt,
                    UpdatedAt,
                    CreatedBy,
                    UpdatedBy,
                    Version
                FROM shifts
                WHERE {whereClause}
                ORDER BY ShiftDate DESC, ShiftStart DESC
                LIMIT @PageSize OFFSET @Offset";

            var shifts = await connection.QueryAsync<ShiftDto>(sql, parameters);
            var shiftsList = shifts.ToList();

            logger.LogInformation(
                "Retrieved {Count} shifts for page {PageNumber}",
                shiftsList.Count,
                request.PageNumber);

            return new GetAllShiftsResult
            {
                Success = true,
                Shifts = shiftsList,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalPages = totalPages
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all shifts");

            return new GetAllShiftsResult
            {
                Success = false,
                Shifts = new List<ShiftDto>(),
                TotalCount = 0,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalPages = 0,
                ErrorMessage = $"Failed to get shifts: {ex.Message}"
            };
        }
    }
}
