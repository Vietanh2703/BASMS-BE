namespace Shifts.API.ShiftsHandler.GetShiftIssueByGuardId;

public record GetShiftIssueByGuardIdQuery(Guid GuardId) : IQuery<GetShiftIssueByGuardIdResult>;

public record GetShiftIssueByGuardIdResult
{
    public bool Success { get; init; }
    public Guid GuardId { get; init; }
    public List<ShiftIssueDto> Issues { get; init; } = new();
    public int TotalIssues { get; init; }
    public string? ErrorMessage { get; init; }
}


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
    public string? EvidenceFilePresignedUrl { get; init; }
    public int TotalShiftsAffected { get; init; }
    public int TotalGuardsAffected { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid CreatedBy { get; init; }
}


internal class GetShiftIssueByGuardIdHandler(
    IDbConnectionFactory dbFactory,
    IS3Service s3Service,
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

            var results = await connection.QueryAsync<dynamic>(sql, new { GuardId = request.GuardId });
            var rawIssues = results.ToList();

            logger.LogInformation(
                "Found {Count} shift issues for Guard {GuardId}",
                rawIssues.Count,
                request.GuardId);

            var issuesWithPresignedUrls = new List<ShiftIssueDto>();

            foreach (dynamic issue in rawIssues)
            {
                string? presignedUrl = null;
                string? evidenceFileUrl = issue.EvidenceFileUrl;
                Guid issueId = issue.Id;

                if (!string.IsNullOrEmpty(evidenceFileUrl))
                {
                    try
                    {
                        presignedUrl = s3Service.GetPresignedUrl(evidenceFileUrl, expirationMinutes: 15);
                        logger.LogInformation(
                            "Generated presigned URL for issue {IssueId}",
                            issueId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "Failed to generate presigned URL for issue {IssueId}, file: {FileUrl}",
                            issueId,
                            evidenceFileUrl);
                    }
                }

                issuesWithPresignedUrls.Add(new ShiftIssueDto
                {
                    Id = issueId,
                    ShiftId = issue.ShiftId,
                    GuardId = issue.GuardId,
                    IssueType = issue.IssueType ?? string.Empty,
                    Reason = issue.Reason ?? string.Empty,
                    StartDate = issue.StartDate,
                    EndDate = issue.EndDate,
                    IssueDate = issue.IssueDate,
                    EvidenceFileUrl = evidenceFileUrl,
                    EvidenceFilePresignedUrl = presignedUrl,
                    TotalShiftsAffected = issue.TotalShiftsAffected,
                    TotalGuardsAffected = issue.TotalGuardsAffected,
                    CreatedAt = issue.CreatedAt,
                    CreatedBy = issue.CreatedBy
                });
            }

            return new GetShiftIssueByGuardIdResult
            {
                Success = true,
                GuardId = request.GuardId,
                Issues = issuesWithPresignedUrls,
                TotalIssues = issuesWithPresignedUrls.Count
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
