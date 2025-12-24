namespace Shifts.API.BackgroundJobs;

public class AutoGenerateShiftsJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoGenerateShiftsJob> _logger;
    private readonly TimeSpan _dailyRunTime = new TimeSpan(2, 0, 0);
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public AutoGenerateShiftsJob(
        IServiceScopeFactory scopeFactory,
        ILogger<AutoGenerateShiftsJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AutoGenerateShiftsJob started. Scheduled to run daily at {RunTime} Vietnam Time (UTC+7)",
            _dailyRunTime);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowVietnam = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
                var nextRun = CalculateNextRunTime(nowVietnam);
                var delay = nextRun - nowVietnam;

                _logger.LogInformation(
                    "Next auto-generate run at: {NextRun} Vietnam Time (in {Hours}h {Minutes}m)",
                    nextRun.ToString("yyyy-MM-dd HH:mm:ss"),
                    (int)delay.TotalHours,
                    delay.Minutes);
                
                await Task.Delay(delay, stoppingToken);
                await CheckAndGenerateShiftsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AutoGenerateShiftsJob");
            }
        }
    }
    
    private DateTime CalculateNextRunTime(DateTime nowVietnam)
    {
        var today2AM = nowVietnam.Date + _dailyRunTime;

        if (nowVietnam < today2AM)
        {
            return today2AM;
        }
        else
        {
            return today2AM.AddDays(1);
        }
    }

    private async Task CheckAndGenerateShiftsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        using var connection = await dbFactory.CreateConnectionAsync();

        _logger.LogInformation("=== AUTO-GENERATE SHIFTS JOB STARTED ===");
        _logger.LogInformation("Checking contracts that need shift generation...");
        
        var contractsNeedGeneration = await FindContractsNeedingGeneration(connection);

        if (!contractsNeedGeneration.Any())
        {
            _logger.LogInformation("✓ No contracts need shift generation at this time.");
            _logger.LogInformation("=== AUTO-GENERATE SHIFTS JOB COMPLETED ===");
            return;
        }

        _logger.LogInformation(
            "✓ Found {Count} contracts that need shift generation",
            contractsNeedGeneration.Count);
        
        var totalShiftsCreated = 0;
        var successCount = 0;
        var failedCount = 0;

        foreach (var contract in contractsNeedGeneration)
        {
            try
            {
                var result = await GenerateShiftsForContract(
                    connection,
                    mediator,
                    contract,
                    cancellationToken);

                if (result.Success)
                {
                    successCount++;
                    totalShiftsCreated += result.ShiftsCreated;

                    _logger.LogInformation(
                        "✓ Contract {ContractNumber}: Generated {Count} shifts",
                        contract.ContractNumber,
                        result.ShiftsCreated);
                }
                else
                {
                    failedCount++;

                    _logger.LogError(
                        "✗ Contract {ContractNumber}: Generation failed - {Errors}",
                        contract.ContractNumber,
                        string.Join(", ", result.Errors));
                }
            }
            catch (Exception ex)
            {
                failedCount++;

                _logger.LogError(ex,
                    "✗ Failed to auto-generate shifts for Contract {ContractNumber}",
                    contract.ContractNumber);
            }
        }
        
        _logger.LogInformation(
            @"=== AUTO-GENERATE SHIFTS JOB COMPLETED ===
              - Contracts Processed: {Total}
              - Success: {Success}
              - Failed: {Failed}
              - Total Shifts Created: {ShiftsCreated}",
            contractsNeedGeneration.Count,
            successCount,
            failedCount,
            totalShiftsCreated);
    }
    
    private async Task<List<ContractNeedingGeneration>> FindContractsNeedingGeneration(
        IDbConnection connection)
    {
        var query = @"
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

        var candidates = await connection.QueryAsync<dynamic>(query);

        var contractsNeedingGeneration = new List<ContractNeedingGeneration>();
        
        using var scope = _scopeFactory.CreateScope();
        var contractClient = scope.ServiceProvider.GetRequiredService<IRequestClient<GetContractRequest>>();

        foreach (var candidate in candidates)
        {
            try
            {
                var contractId = (Guid)candidate.ContractId;
                
                var response = await contractClient.GetResponse<GetContractResponse>(
                    new GetContractRequest { ContractId = contractId },
                    timeout: RequestTimeout.After(s: 5));

                var contractResponse = response.Message;
                
                if (!contractResponse.Success || contractResponse.Contract == null)
                {
                    _logger.LogWarning(
                        "Failed to get contract info for {ContractId}: {Error}",
                        contractId,
                        contractResponse.ErrorMessage);
                    continue;
                }

                var contract = contractResponse.Contract;
                
                if (!contract.IsActive)
                {
                    _logger.LogDebug(
                        "Skipping Contract {ContractNumber} - not active",
                        contract.ContractNumber);
                    continue;
                }

                if (contract.EndDate < DateTime.UtcNow.Date)
                {
                    _logger.LogDebug(
                        "Skipping Contract {ContractNumber} - expired on {EndDate}",
                        contract.ContractNumber,
                        contract.EndDate.ToString("yyyy-MM-dd"));
                    continue;
                }

                if (!contract.AutoGenerateShifts)
                {
                    _logger.LogDebug(
                        "Skipping Contract {ContractNumber} - AutoGenerateShifts disabled",
                        contract.ContractNumber);
                    continue;
                }
                
                contractsNeedingGeneration.Add(new ContractNeedingGeneration
                {
                    ContractId = contract.Id,
                    ContractNumber = contract.ContractNumber,
                    ContractEndDate = contract.EndDate,
                    GenerateShiftsAdvanceDays = contract.GenerateShiftsAdvanceDays,
                    DefaultManagerId = contract.CreatedBy ?? Guid.Empty,
                    LastShiftDate = candidate.LastShiftDate,
                    TemplateCount = (int)(long)candidate.TemplateCount,
                    ExistingShiftsCount = (int)(long)candidate.ExistingShiftsCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to get contract info for ContractId: {ContractId}",
                    (Guid)candidate.ContractId);
            }
        }

        return contractsNeedingGeneration;
    }

    private async Task<GenerationResult> GenerateShiftsForContract(
        IDbConnection connection,
        ISender mediator,
        ContractNeedingGeneration contract,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing Contract {ContractNumber} (ID: {ContractId})",
            contract.ContractNumber,
            contract.ContractId);

        _logger.LogInformation(
            "  - Contract End Date: {EndDate}",
            contract.ContractEndDate.ToString("yyyy-MM-dd"));

        _logger.LogInformation(
            "  - Last Shift Date: {LastShiftDate}",
            contract.LastShiftDate?.ToString("yyyy-MM-dd") ?? "None");

        _logger.LogInformation(
            "  - Template Count: {TemplateCount}",
            contract.TemplateCount);

        _logger.LogInformation(
            "  - Existing Shifts: {ExistingShiftsCount}",
            contract.ExistingShiftsCount);
        
        DateTime generateFrom;
        DateTime generateTo;

        if (contract.LastShiftDate.HasValue)
        {
            generateFrom = contract.LastShiftDate.Value.AddDays(1);
        }
        else
        {
            generateFrom = DateTime.UtcNow.Date;
        }
        
        var advanceDays = contract.GenerateShiftsAdvanceDays;
        generateTo = generateFrom.AddDays(advanceDays);

        // Always cap at contract end date + 1 (to include the end date in generation)
        // The GenerateDateRange loop uses "date < to", so we need +1 to make end date inclusive
        var maxAllowedGenerateTo = contract.ContractEndDate.AddDays(1);

        if (generateTo > maxAllowedGenerateTo)
        {
            generateTo = maxAllowedGenerateTo;

            _logger.LogInformation(
                "  - Limiting generation to contract end date: {EndDate}",
                contract.ContractEndDate.ToString("yyyy-MM-dd"));
        }

        var daysToGenerate = (generateTo - generateFrom).Days;

        _logger.LogInformation(
            "  - Generate From: {From}",
            generateFrom.ToString("yyyy-MM-dd"));

        _logger.LogInformation(
            "  - Generate To: {To}",
            generateTo.ToString("yyyy-MM-dd"));

        _logger.LogInformation(
            "  - Days to Generate: {Days}",
            daysToGenerate);

        var templateIds = await connection.QueryAsync<Guid>(
            @"SELECT Id FROM shift_templates
              WHERE ContractId = @ContractId
                AND IsActive = 1
                AND IsDeleted = 0",
            new { ContractId = contract.ContractId });

        var templateIdList = templateIds.ToList();

        if (!templateIdList.Any())
        {
            _logger.LogWarning(
                "  - No active templates found for Contract {ContractNumber}",
                contract.ContractNumber);

            return new GenerationResult
            {
                Success = false,
                Errors = new List<string> { "No active templates found" }
            };
        }

        _logger.LogInformation(
            "  - Template IDs: {TemplateIds}",
            string.Join(", ", templateIdList.Take(5)) +
            (templateIdList.Count > 5 ? $" ... ({templateIdList.Count} total)" : ""));
        
        var managerId = await FindManagerWithPermission(connection, contract.DefaultManagerId);

        if (managerId == Guid.Empty)
        {
            _logger.LogError(
                "  - No manager with CanCreateShifts permission found for Contract {ContractNumber}",
                contract.ContractNumber);

            return new GenerationResult
            {
                Success = false,
                Errors = new List<string> { "No manager with permission found" }
            };
        }
        
        var command = new GenerateShiftsCommand(
            ManagerId: managerId,
            ShiftTemplateIds: templateIdList,
            GenerateFromDate: generateFrom,
            GenerateDays: daysToGenerate
        );

        var result = await mediator.Send(command, cancellationToken);

        return new GenerationResult
        {
            Success = result.Errors.Count == 0,
            ShiftsCreated = result.ShiftsCreatedCount,
            ShiftsSkipped = result.ShiftsSkippedCount,
            Errors = result.Errors
        };
    }

    private async Task<Guid> FindManagerWithPermission(
        IDbConnection connection,
        Guid defaultManagerId)
    {
        if (defaultManagerId != Guid.Empty)
        {
            var hasPermission = await connection.QueryFirstOrDefaultAsync<bool>(
                @"SELECT CanCreateShifts FROM managers
                  WHERE Id = @ManagerId
                    AND IsActive = 1
                    AND IsDeleted = 0",
                new { ManagerId = defaultManagerId });

            if (hasPermission)
            {
                return defaultManagerId;
            }
        }
        
        var anyManager = await connection.QueryFirstOrDefaultAsync<Guid?>(
            @"SELECT Id FROM managers
              WHERE CanCreateShifts = 1
                AND IsActive = 1
                AND IsDeleted = 0
              LIMIT 1");

        return anyManager ?? Guid.Empty;
    }
    
    private class ContractNeedingGeneration
    {
        public Guid ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public DateTime ContractEndDate { get; set; }
        public int GenerateShiftsAdvanceDays { get; set; }
        public Guid DefaultManagerId { get; set; }
        public DateTime? LastShiftDate { get; set; }
        public int TemplateCount { get; set; }
        public int ExistingShiftsCount { get; set; }
    }
    
    private class GenerationResult
    {
        public bool Success { get; set; }
        public int ShiftsCreated { get; set; }
        public int ShiftsSkipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
