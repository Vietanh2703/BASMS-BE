namespace Contracts.API.ContractsHandler.UpdateHolidayPolicy;

// ================================================================
// COMMAND & RESULT
// ================================================================

/// <summary>
/// Command để update public holiday policy
/// </summary>
public record UpdateHolidayPolicyCommand : ICommand<UpdateHolidayPolicyResult>
{
    public Guid HolidayId { get; init; }
    public Guid? ContractId { get; init; }
    public DateTime HolidayDate { get; init; }
    public string HolidayName { get; init; } = string.Empty;
    public string? HolidayNameEn { get; init; }
    public string HolidayCategory { get; init; } = string.Empty;

    // Tet Period
    public bool IsTetPeriod { get; init; }
    public bool IsTetHoliday { get; init; }
    public int? TetDayNumber { get; init; }
    public DateTime? HolidayStartDate { get; init; }
    public DateTime? HolidayEndDate { get; init; }
    public int? TotalHolidayDays { get; init; }

    // Official & Observed
    public bool IsOfficialHoliday { get; init; }
    public bool IsObserved { get; init; }
    public DateTime? OriginalDate { get; init; }
    public DateTime? ObservedDate { get; init; }

    // Scope
    public bool AppliesNationwide { get; init; }
    public string? AppliesToRegions { get; init; }

    // Impact
    public bool StandardWorkplacesClosed { get; init; }
    public bool EssentialServicesOperating { get; init; }

    public string? Description { get; init; }
    public int Year { get; init; }
}

/// <summary>
/// Kết quả update holiday policy
/// </summary>
public record UpdateHolidayPolicyResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? HolidayId { get; init; }
    public string? HolidayName { get; init; }
    public DateTime? HolidayDate { get; init; }
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// Handler để update public holiday policy
/// </summary>
internal class UpdateHolidayPolicyHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<UpdateHolidayPolicyHandler> logger)
    : ICommandHandler<UpdateHolidayPolicyCommand, UpdateHolidayPolicyResult>
{
    public async Task<UpdateHolidayPolicyResult> Handle(
        UpdateHolidayPolicyCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Updating public holiday: {HolidayId}", request.HolidayId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // 1. CHECK IF HOLIDAY EXISTS
            // ================================================================
            var checkQuery = @"
                SELECT HolidayName, HolidayDate
                FROM public_holidays
                WHERE Id = @HolidayId
            ";

            var existingHoliday = await connection.QuerySingleOrDefaultAsync<dynamic>(
                checkQuery,
                new { HolidayId = request.HolidayId });

            if (existingHoliday == null)
            {
                logger.LogWarning("Public holiday not found: {HolidayId}", request.HolidayId);
                return new UpdateHolidayPolicyResult
                {
                    Success = false,
                    ErrorMessage = $"Public holiday with ID {request.HolidayId} not found"
                };
            }

            // ================================================================
            // 2. VALIDATE HOLIDAY DATE UNIQUENESS (if changed)
            // ================================================================
            var dateCheckQuery = @"
                SELECT COUNT(*)
                FROM public_holidays
                WHERE HolidayDate = @HolidayDate
                AND Year = @Year
                AND Id != @HolidayId
                AND (ContractId = @ContractId OR (ContractId IS NULL AND @ContractId IS NULL))
            ";

            var dateExists = await connection.ExecuteScalarAsync<int>(
                dateCheckQuery,
                new
                {
                    HolidayDate = request.HolidayDate,
                    Year = request.Year,
                    HolidayId = request.HolidayId,
                    ContractId = request.ContractId
                });

            if (dateExists > 0)
            {
                logger.LogWarning(
                    "Holiday date {HolidayDate} already exists for year {Year}",
                    request.HolidayDate,
                    request.Year);
                return new UpdateHolidayPolicyResult
                {
                    Success = false,
                    ErrorMessage = $"A holiday on {request.HolidayDate:yyyy-MM-dd} already exists for year {request.Year}"
                };
            }

            // ================================================================
            // 3. UPDATE PUBLIC HOLIDAY
            // ================================================================
            var updateQuery = @"
                UPDATE public_holidays SET
                    ContractId = @ContractId,
                    HolidayDate = @HolidayDate,
                    HolidayName = @HolidayName,
                    HolidayNameEn = @HolidayNameEn,
                    HolidayCategory = @HolidayCategory,
                    IsTetPeriod = @IsTetPeriod,
                    IsTetHoliday = @IsTetHoliday,
                    TetDayNumber = @TetDayNumber,
                    HolidayStartDate = @HolidayStartDate,
                    HolidayEndDate = @HolidayEndDate,
                    TotalHolidayDays = @TotalHolidayDays,
                    IsOfficialHoliday = @IsOfficialHoliday,
                    IsObserved = @IsObserved,
                    OriginalDate = @OriginalDate,
                    ObservedDate = @ObservedDate,
                    AppliesNationwide = @AppliesNationwide,
                    AppliesToRegions = @AppliesToRegions,
                    StandardWorkplacesClosed = @StandardWorkplacesClosed,
                    EssentialServicesOperating = @EssentialServicesOperating,
                    Description = @Description,
                    Year = @Year,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @HolidayId
            ";

            var rowsAffected = await connection.ExecuteAsync(updateQuery, new
            {
                HolidayId = request.HolidayId,
                ContractId = request.ContractId,
                HolidayDate = request.HolidayDate,
                HolidayName = request.HolidayName,
                HolidayNameEn = request.HolidayNameEn,
                HolidayCategory = request.HolidayCategory,
                IsTetPeriod = request.IsTetPeriod,
                IsTetHoliday = request.IsTetHoliday,
                TetDayNumber = request.TetDayNumber,
                HolidayStartDate = request.HolidayStartDate,
                HolidayEndDate = request.HolidayEndDate,
                TotalHolidayDays = request.TotalHolidayDays,
                IsOfficialHoliday = request.IsOfficialHoliday,
                IsObserved = request.IsObserved,
                OriginalDate = request.OriginalDate,
                ObservedDate = request.ObservedDate,
                AppliesNationwide = request.AppliesNationwide,
                AppliesToRegions = request.AppliesToRegions,
                StandardWorkplacesClosed = request.StandardWorkplacesClosed,
                EssentialServicesOperating = request.EssentialServicesOperating,
                Description = request.Description,
                Year = request.Year,
                UpdatedAt = DateTime.UtcNow
            });

            if (rowsAffected == 0)
            {
                logger.LogWarning("No rows affected when updating holiday: {HolidayId}", request.HolidayId);
                return new UpdateHolidayPolicyResult
                {
                    Success = false,
                    ErrorMessage = "Failed to update holiday"
                };
            }

            logger.LogInformation(
                "Successfully updated holiday {HolidayName} (ID: {HolidayId})",
                request.HolidayName, request.HolidayId);

            return new UpdateHolidayPolicyResult
            {
                Success = true,
                HolidayId = request.HolidayId,
                HolidayName = request.HolidayName,
                HolidayDate = request.HolidayDate
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating public holiday: {HolidayId}", request.HolidayId);
            return new UpdateHolidayPolicyResult
            {
                Success = false,
                ErrorMessage = $"Error updating holiday: {ex.Message}"
            };
        }
    }
}
