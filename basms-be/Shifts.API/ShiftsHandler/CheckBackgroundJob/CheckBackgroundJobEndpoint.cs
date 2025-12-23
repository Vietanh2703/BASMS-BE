namespace Shifts.API.ShiftsHandler.CheckBackgroundJob;

public class CheckBackgroundJobEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/background-job/status", async (
            [FromServices] IDbConnectionFactory dbFactory) =>
        {
            using var connection = await dbFactory.CreateConnectionAsync();

            var response = new
            {
                ServerTime = DateTime.UtcNow,
                TotalContractsWithAutoGenerate = await connection.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(DISTINCT st.ContractId)
                    FROM shift_templates st
                    WHERE st.IsActive = 1
                      AND st.IsDeleted = 0
                      AND st.ContractId IS NOT NULL
                "),
                
                ContractsNeedingGeneration = await connection.QueryAsync<dynamic>(@"
                    SELECT
                        st.ContractId,
                        MAX(s.ShiftDate) AS LastShiftDate,
                        DATEDIFF(MAX(s.ShiftDate), CURDATE()) AS DaysRemaining,
                        COUNT(DISTINCT st.Id) AS TemplateCount,
                        COUNT(s.Id) AS ExistingShiftsCount
                    FROM shift_templates st
                    LEFT JOIN shifts s ON s.ContractId = st.ContractId AND s.IsDeleted = 0
                    WHERE st.IsActive = 1
                      AND st.IsDeleted = 0
                      AND st.ContractId IS NOT NULL
                    GROUP BY st.ContractId
                    HAVING MAX(s.ShiftDate) IS NULL
                       OR MAX(s.ShiftDate) <= DATE_ADD(CURDATE(), INTERVAL 7 DAY)
                "),
                
                Stats = new
                {
                    TotalShiftTemplates = await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM shift_templates WHERE IsActive = 1 AND IsDeleted = 0"),

                    TotalShifts = await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM shifts WHERE IsDeleted = 0"),

                    ShiftsCreatedLast24Hours = await connection.ExecuteScalarAsync<int>(@"
                        SELECT COUNT(*) FROM shifts
                        WHERE IsDeleted = 0
                          AND CreatedAt >= DATE_SUB(NOW(), INTERVAL 24 HOUR)
                    "),

                    ShiftsCreatedLast7Days = await connection.ExecuteScalarAsync<int>(@"
                        SELECT COUNT(*) FROM shifts
                        WHERE IsDeleted = 0
                          AND CreatedAt >= DATE_SUB(NOW(), INTERVAL 7 DAY)
                    "),

                    FutureShifts = await connection.ExecuteScalarAsync<int>(@"
                        SELECT COUNT(*) FROM shifts
                        WHERE IsDeleted = 0
                          AND ShiftDate >= CURDATE()
                    ")
                },
                
                ManagersWithPermission = await connection.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(*) FROM managers
                    WHERE CanCreateShifts = 1
                      AND IsActive = 1
                      AND IsDeleted = 0
                "),

                JobInfo = new
                {
                    JobName = "AutoGenerateShiftsJob",
                    ScheduledTime = "Daily at 2:00 AM",
                    TriggerCondition = "When shifts remaining <= 7 days",
                    GenerationWindow = "30 days advance",
                    Status = "Check logs to confirm if job is running"
                }
            };

            return Results.Ok(response);
        })
        .WithName("CheckBackgroundJobStatus")
        .WithTags("Shifts - Background Jobs")
        .WithSummary("Check background job status and diagnostics")
        .WithDescription("Returns information about auto-generate shifts background job");
        
        app.MapPost("/api/shifts/background-job/trigger-manual", async (
            [FromServices] IDbConnectionFactory dbFactory,
            [FromServices] IRequestClient<GetContractRequest> contractClient,
            [FromServices] ILogger<CheckBackgroundJobEndpoint> logger) =>
        {
            using var connection = await dbFactory.CreateConnectionAsync();

            logger.LogInformation("=== MANUAL TRIGGER: Background Job Simulation ===");
            
            var contractsQuery = @"
                SELECT
                    st.ContractId,
                    MAX(s.ShiftDate) AS LastShiftDate,
                    COUNT(DISTINCT st.Id) AS TemplateCount,
                    COUNT(s.Id) AS ExistingShiftsCount
                FROM shift_templates st
                LEFT JOIN shifts s ON s.ContractId = st.ContractId AND s.IsDeleted = 0
                WHERE st.IsActive = 1
                  AND st.IsDeleted = 0
                  AND st.ContractId IS NOT NULL
                GROUP BY st.ContractId
                HAVING MAX(s.ShiftDate) IS NULL
                   OR MAX(s.ShiftDate) <= DATE_ADD(CURDATE(), INTERVAL 7 DAY)";

            var candidates = await connection.QueryAsync<dynamic>(contractsQuery);
            var candidatesList = candidates.ToList();

            if (!candidatesList.Any())
            {
                return Results.Ok(new
                {
                    Success = true,
                    Message = "No contracts need shift generation at this time",
                    ContractsChecked = 0,
                    ContractsNeedingGeneration = 0
                });
            }

            var results = new List<object>();

            foreach (var candidate in candidatesList)
            {
                try
                {
                    var contractId = (Guid)candidate.ContractId;
                    var response = await contractClient.GetResponse<GetContractResponse>(
                        new GetContractRequest { ContractId = contractId },
                        timeout: RequestTimeout.After(s: 10));

                    var contractResponse = response.Message;

                    if (!contractResponse.Success || contractResponse.Contract == null)
                    {
                        results.Add(new
                        {
                            ContractId = contractId,
                            Status = "FAILED",
                            Reason = "Failed to fetch contract info",
                            Error = contractResponse.ErrorMessage
                        });
                        continue;
                    }

                    var contract = contractResponse.Contract;
                    var templateIds = await connection.QueryAsync<Guid>(
                        @"SELECT Id FROM shift_templates
                          WHERE ContractId = @ContractId
                            AND IsActive = 1
                            AND IsDeleted = 0",
                        new { ContractId = contractId });

                    var templateIdList = templateIds.ToList();

                    results.Add(new
                    {
                        ContractId = contractId,
                        ContractNumber = contract.ContractNumber,
                        Status = contract.IsActive ? "READY" : "INACTIVE",
                        AutoGenerateEnabled = contract.AutoGenerateShifts,
                        LastShiftDate = candidate.LastShiftDate,
                        TemplateCount = (int)(long)candidate.TemplateCount,
                        TemplateIds = templateIdList,
                        ExistingShiftsCount = (int)(long)candidate.ExistingShiftsCount,
                        ContractEndDate = contract.EndDate,
                        AdvanceDays = contract.GenerateShiftsAdvanceDays,
                        ShouldGenerate = contract.IsActive && contract.AutoGenerateShifts,
                        Recommendation = contract.IsActive && contract.AutoGenerateShifts
                            ? $"Use POST /api/shifts/generate with TemplateIds: {string.Join(", ", templateIdList.Take(3))}"
                            : "Contract not eligible for auto-generation"
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing contract {ContractId}", (Guid)candidate.ContractId);
                    results.Add(new
                    {
                        ContractId = (Guid)candidate.ContractId,
                        Status = "ERROR",
                        Error = ex.Message
                    });
                }
            }

            return Results.Ok(new
            {
                Success = true,
                Message = "Manual trigger check completed",
                Timestamp = DateTime.UtcNow,
                ContractsChecked = candidatesList.Count,
                Results = results
            });
        })
        .WithName("TriggerBackgroundJobManual")
        .WithTags("Shifts - Background Jobs")
        .WithSummary("Manually trigger background job check (for testing)");


        app.MapGet("/api/shifts/background-job/check-contract/{contractId:guid}", async (
            [FromRoute] Guid contractId,
            [FromServices] IDbConnectionFactory dbFactory,
            [FromServices] IRequestClient<GetContractRequest> contractClient) =>
        {
            using var connection = await dbFactory.CreateConnectionAsync();

            var response = await contractClient.GetResponse<GetContractResponse>(
                new GetContractRequest { ContractId = contractId },
                timeout: RequestTimeout.After(s: 10));

            var contractResponse = response.Message;

            if (!contractResponse.Success || contractResponse.Contract == null)
            {
                return Results.NotFound(new
                {
                    Success = false,
                    Message = "Contract not found",
                    Error = contractResponse.ErrorMessage
                });
            }

            var contract = contractResponse.Contract;
            
            var templates = await connection.QueryAsync<dynamic>(@"
                SELECT
                    Id,
                    TemplateCode,
                    TemplateName,
                    IsActive,
                    EffectiveFrom,
                    EffectiveTo
                FROM shift_templates
                WHERE ContractId = @ContractId
                  AND IsDeleted = 0
                ORDER BY IsActive DESC, TemplateName
            ", new { ContractId = contractId });
            
            var shiftStats = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT
                    COUNT(*) AS TotalShifts,
                    MIN(ShiftDate) AS FirstShiftDate,
                    MAX(ShiftDate) AS LastShiftDate,
                    DATEDIFF(MAX(ShiftDate), CURDATE()) AS DaysRemaining
                FROM shifts
                WHERE ContractId = @ContractId
                  AND IsDeleted = 0
            ", new { ContractId = contractId });

            var lastShiftDate = shiftStats?.LastShiftDate as DateTime?;
            var daysRemaining = shiftStats?.DaysRemaining as int? ?? 0;

            var shouldTrigger = lastShiftDate == null || daysRemaining <= 7;

            return Results.Ok(new
            {
                Success = true,
                Contract = new
                {
                    contract.Id,
                    contract.ContractNumber,
                    contract.IsActive,
                    contract.AutoGenerateShifts,
                    contract.StartDate,
                    contract.EndDate,
                    contract.GenerateShiftsAdvanceDays
                },
                Templates = templates,
                ShiftStatistics = new
                {
                    TotalShifts = shiftStats?.TotalShifts ?? 0,
                    FirstShiftDate = shiftStats?.FirstShiftDate,
                    LastShiftDate = lastShiftDate,
                    DaysRemaining = daysRemaining
                },
                BackgroundJobAnalysis = new
                {
                    WillTrigger = shouldTrigger,
                    Reason = shouldTrigger
                        ? (lastShiftDate == null
                            ? "No shifts exist yet"
                            : $"Only {daysRemaining} days of shifts remaining (threshold: 7 days)")
                        : $"Still have {daysRemaining} days of shifts (threshold: 7 days)",
                    NextGenerationDate = lastShiftDate?.AddDays(1),
                    RecommendedAction = shouldTrigger && contract.AutoGenerateShifts
                        ? "Background job will auto-generate shifts at next run (2:00 AM daily)"
                        : "No action needed"
                }
            });
        })
        .WithName("CheckContractBackgroundJob")
        .WithTags("Shifts - Background Jobs")
        .WithSummary("Check if specific contract will trigger background job");
    }
}
