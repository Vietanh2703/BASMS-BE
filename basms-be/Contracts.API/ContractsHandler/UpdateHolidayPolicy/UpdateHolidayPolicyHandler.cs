namespace Contracts.API.ContractsHandler.UpdateHolidayPolicy;

public record UpdateHolidayPolicyCommand : ICommand<UpdateHolidayPolicyResult>
{
    public Guid HolidayId { get; init; }
    public Guid? ContractId { get; init; }
    public DateTime HolidayDate { get; init; }
    public string HolidayName { get; init; } = string.Empty;
    public string? HolidayNameEn { get; init; }
    public string HolidayCategory { get; init; } = string.Empty;
    public bool IsTetPeriod { get; init; }
    public bool IsTetHoliday { get; init; }
    public int? TetDayNumber { get; init; }
    public DateTime? HolidayStartDate { get; init; }
    public DateTime? HolidayEndDate { get; init; }
    public int? TotalHolidayDays { get; init; }
    public bool IsOfficialHoliday { get; init; }
    public bool IsObserved { get; init; }
    public DateTime? OriginalDate { get; init; }
    public DateTime? ObservedDate { get; init; }
    public bool AppliesNationwide { get; init; }
    public string? AppliesToRegions { get; init; }
    public bool StandardWorkplacesClosed { get; init; }
    public bool EssentialServicesOperating { get; init; }
    public string? Description { get; init; }
    public int Year { get; init; }
}


public record UpdateHolidayPolicyResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? HolidayId { get; init; }
    public string? HolidayName { get; init; }
    public DateTime? HolidayDate { get; init; }
}


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
            
            var checkQuery = @"
                SELECT HolidayName, HolidayDate
                FROM public_holidays
                WHERE Id = @HolidayId
            ";

            var existingHoliday = await connection.QuerySingleOrDefaultAsync<dynamic>(
                checkQuery,
                new { request.HolidayId });

            if (existingHoliday == null)
            {
                logger.LogWarning("Public holiday not found: {HolidayId}", request.HolidayId);
                return new UpdateHolidayPolicyResult
                {
                    Success = false,
                    ErrorMessage = $"Public holiday with ID {request.HolidayId} not found"
                };
            }

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
