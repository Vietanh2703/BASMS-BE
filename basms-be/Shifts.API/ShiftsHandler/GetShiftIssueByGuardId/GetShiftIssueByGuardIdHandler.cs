using Dapper;
using Shifts.API.Data;

namespace Shifts.API.ShiftsHandler.GetShiftIssueByGuardId;

/// <summary>
/// Query để lấy danh sách shift issues của một guard
/// </summary>
public record GetShiftIssueByGuardIdQuery(Guid GuardId) : IQuery<GetShiftIssueByGuardIdResult>;

/// <summary>
/// Result chứa danh sách shift issues của guard
/// </summary>
public record GetShiftIssueByGuardIdResult
{
    public bool Success { get; init; }
    public Guid GuardId { get; init; }
    public List<ShiftIssueDto> Issues { get; init; } = new();
    public int TotalIssues { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO cho shift issue
/// </summary>
public record ShiftIssueDto
{
    public Guid Id { get; init; }
    public Guid? ShiftId { get; init; }
    public Guid? GuardId { get; init; }
    public string IssueType { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime IssueDate { get; init; }
    public string? EvidenceFileUrl { get; init; }
    public int TotalShiftsAffected { get; init; }
    public int TotalGuardsAffected { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid CreatedBy { get; init; }
}

/// <summary>
/// Handler để lấy danh sách shift issues của một guard cụ thể
/// Sắp xếp theo IssueDate giảm dần (mới nhất trước)
/// </summary>
internal class GetShiftIssueByGuardIdHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetShiftIssueByGuardIdHandler> logger)
    : IQueryHandler<GetShiftIssueByGuardIdQuery, GetShiftIssueByGuardIdResult>
{
    public async Task<GetShiftIssueByGuardIdResult> Handle(
        GetShiftIssueByGuardIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting shift issues for Guard {GuardId}",
                request.GuardId);

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // KIỂM TRA GUARD CÓ TỒN TẠI KHÔNG
            // ================================================================
            var guardExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT COUNT(*) > 0 FROM guards WHERE Id = @GuardId AND IsDeleted = 0",
                new { GuardId = request.GuardId });

            if (!guardExists)
            {
                logger.LogWarning("Guard {GuardId} not found", request.GuardId);
                return new GetShiftIssueByGuardIdResult
                {
                    Success = false,
                    GuardId = request.GuardId,
                    ErrorMessage = $"Guard {request.GuardId} not found"
                };
            }

            // ================================================================
            // SQL QUERY - LẤY DANH SÁCH SHIFT ISSUES
            // ================================================================
            // Logic:
            // 1. Lấy tất cả shift_issues có GuardId khớp
            // 2. Sắp xếp theo IssueDate giảm dần (mới nhất trước)
            // ================================================================

            var sql = @"
                SELECT
                    Id,
                    ShiftId,
                    GuardId,
                    IssueType,
                    Reason,
                    StartDate,
                    EndDate,
                    IssueDate,
                    EvidenceFileUrl,
                    TotalShiftsAffected,
                    TotalGuardsAffected,
                    CreatedAt,
                    CreatedBy
                FROM shift_issues
                WHERE
                    GuardId = @GuardId
                    AND IsDeleted = 0
                ORDER BY
                    IssueDate DESC,
                    CreatedAt DESC";

            var results = await connection.QueryAsync<ShiftIssueDto>(sql, new { GuardId = request.GuardId });
            var issuesList = results.ToList();

            logger.LogInformation(
                "Found {Count} shift issues for Guard {GuardId}",
                issuesList.Count,
                request.GuardId);

            return new GetShiftIssueByGuardIdResult
            {
                Success = true,
                GuardId = request.GuardId,
                Issues = issuesList,
                TotalIssues = issuesList.Count
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting shift issues for Guard {GuardId}",
                request.GuardId);

            return new GetShiftIssueByGuardIdResult
            {
                Success = false,
                GuardId = request.GuardId,
                ErrorMessage = $"Failed to get shift issues: {ex.Message}"
            };
        }
    }
}
