using Dapper;
using MediatR;
using MassTransit;
using BuildingBlocks.Messaging.Events;
using Shifts.API.Data;
using Shifts.API.ShiftsHandler.GenerateShifts;

namespace Shifts.API.BackgroundJobs;

/// <summary>
/// OPTIMIZED Background job tự động tạo shifts
///
/// WORKFLOW LOGIC:
/// 1. Chạy mỗi ngày lúc 2:00 AM
/// 2. Tìm contracts có AutoGenerateShifts = true
/// 3. Check: Còn <= 7 ngày nữa hết shifts?
/// 4. Nếu có → Tự động tạo tiếp 30 ngày
/// 5. Giới hạn: Không tạo quá EndDate của contract
///
/// VÍ DỤ:
/// - Contract: 01/01/2025 → 31/12/2025
/// - Lần 1 (01/01): Tạo ca từ 01/01 → 31/01 (30 ngày)
/// - Lần 2 (25/01): Còn 7 ngày → Tạo tiếp 01/02 → 03/03 (30 ngày)
/// - Lần 3 (24/02): Còn 7 ngày → Tạo tiếp 04/03 → 03/04 (30 ngày)
/// - ...
/// - Lần cuối (02/12): Tạo đến 31/12 (EndDate) rồi DỪNG
///
/// PERFORMANCE:
/// - Xử lý nhiều contracts song song
/// - Batch generation cho tất cả templates của contract
/// - Sử dụng optimized handler (100x faster)
/// </summary>
public class AutoGenerateShiftsJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoGenerateShiftsJob> _logger;

    // Schedule: Run at 2:00 AM Vietnam Time (UTC+7) every day
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

                // Wait until next scheduled run
                await Task.Delay(delay, stoppingToken);

                // Execute the job
                await CheckAndGenerateShiftsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AutoGenerateShiftsJob");
            }
        }
    }

    /// <summary>
    /// Calculate next run time (2:00 AM Vietnam Time today or tomorrow)
    /// </summary>
    private DateTime CalculateNextRunTime(DateTime nowVietnam)
    {
        var today2AM = nowVietnam.Date + _dailyRunTime;

        if (nowVietnam < today2AM)
        {
            return today2AM; // Run today at 2 AM Vietnam Time
        }
        else
        {
            return today2AM.AddDays(1); // Run tomorrow at 2 AM Vietnam Time
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

        // ================================================================
        // BƯỚC 1: TÌM CÁC CONTRACT CẦN GENERATE SHIFTS
        // ================================================================
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

        // ================================================================
        // BƯỚC 2: GENERATE SHIFTS CHO TỪNG CONTRACT
        // ================================================================
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

        // ================================================================
        // BƯỚC 3: SUMMARY
        // ================================================================
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

    /// <summary>
    /// Find contracts that need shift generation
    ///
    /// CRITERIA:
    /// 1. Has shift templates với ContractId
    /// 2. (Chưa có shifts HOẶC Ca cuối cùng còn <= 7 ngày)
    /// 3. Sau đó query Contracts.API qua RabbitMQ để verify IsActive, EndDate, etc.
    /// </summary>
    private async Task<List<ContractNeedingGeneration>> FindContractsNeedingGeneration(
        IDbConnection connection)
    {
        // STEP 1: Find contracts with templates that need generation
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

        // STEP 2: For each candidate, get contract info from Contracts.API via RabbitMQ
        using var scope = _scopeFactory.CreateScope();
        var contractClient = scope.ServiceProvider.GetRequiredService<IRequestClient<GetContractRequest>>();

        foreach (var candidate in candidates)
        {
            try
            {
                var contractId = (Guid)candidate.ContractId;

                // Query Contracts.API
                var response = await contractClient.GetResponse<GetContractResponse>(
                    new GetContractRequest { ContractId = contractId },
                    timeout: RequestTimeout.After(s: 5));

                var contractResponse = response.Message;

                // Check if request was successful
                if (!contractResponse.Success || contractResponse.Contract == null)
                {
                    _logger.LogWarning(
                        "Failed to get contract info for {ContractId}: {Error}",
                        contractId,
                        contractResponse.ErrorMessage);
                    continue;
                }

                var contract = contractResponse.Contract;

                // Verify contract is active and not expired
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

                // Add to list
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

    /// <summary>
    /// Generate shifts for một contract
    /// </summary>
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

        // ================================================================
        // CALCULATE GENERATION DATE RANGE
        // ================================================================
        DateTime generateFrom;
        DateTime generateTo;

        if (contract.LastShiftDate.HasValue)
        {
            // Có shifts rồi → tạo tiếp từ ngày sau ca cuối
            generateFrom = contract.LastShiftDate.Value.AddDays(1);
        }
        else
        {
            // Chưa có shifts → tạo từ hôm nay
            generateFrom = DateTime.UtcNow.Date;
        }

        // Calculate end date: min(from + advance days, contract end date)
        var advanceDays = contract.GenerateShiftsAdvanceDays;
        generateTo = generateFrom.AddDays(advanceDays);

        // Don't generate beyond contract end date
        if (generateTo > contract.ContractEndDate)
        {
            generateTo = contract.ContractEndDate.AddDays(1); // Inclusive

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

        // ================================================================
        // GET ALL TEMPLATES FOR THIS CONTRACT
        // ================================================================
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

        // ================================================================
        // FIND MANAGER WITH PERMISSION
        // ================================================================
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

        // ================================================================
        // EXECUTE GENERATION COMMAND
        // ================================================================
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

    /// <summary>
    /// Find manager with CanCreateShifts permission
    /// </summary>
    private async Task<Guid> FindManagerWithPermission(
        IDbConnection connection,
        Guid defaultManagerId)
    {
        // Try default manager first
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

        // Find any manager with permission
        var anyManager = await connection.QueryFirstOrDefaultAsync<Guid?>(
            @"SELECT Id FROM managers
              WHERE CanCreateShifts = 1
                AND IsActive = 1
                AND IsDeleted = 0
              LIMIT 1");

        return anyManager ?? Guid.Empty;
    }

    /// <summary>
    /// Contract cần generate shifts
    /// </summary>
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

    /// <summary>
    /// Kết quả generation
    /// </summary>
    private class GenerationResult
    {
        public bool Success { get; set; }
        public int ShiftsCreated { get; set; }
        public int ShiftsSkipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
