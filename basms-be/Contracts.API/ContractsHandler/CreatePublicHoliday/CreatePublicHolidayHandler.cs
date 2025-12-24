namespace Contracts.API.ContractsHandler.CreatePublicHoliday;

public record CreatePublicHolidayCommand : ICommand<CreatePublicHolidayResult>
{
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
    public bool IsOfficialHoliday { get; init; } = true;
    public bool IsObserved { get; init; } = true;
    public DateTime? OriginalDate { get; init; }
    public DateTime? ObservedDate { get; init; }
    public bool AppliesNationwide { get; init; } = true;
    public string? AppliesToRegions { get; init; }
    public bool StandardWorkplacesClosed { get; init; } = true;
    public bool EssentialServicesOperating { get; init; } = true;
    public string? Description { get; init; }
    public int Year { get; init; }
}

public record CreatePublicHolidayResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? HolidayId { get; init; }
    public string? HolidayName { get; init; }
    public DateTime? HolidayDate { get; init; }
}


internal class CreatePublicHolidayHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<CreatePublicHolidayHandler> logger)
    : ICommandHandler<CreatePublicHolidayCommand, CreatePublicHolidayResult>
{
    public async Task<CreatePublicHolidayResult> Handle(
        CreatePublicHolidayCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Creating public holiday: {HolidayName} on {HolidayDate}",
                request.HolidayName, request.HolidayDate);

            using var connection = await connectionFactory.CreateConnectionAsync();

            if (request.ContractId.HasValue)
            {
                var contractCheckQuery = @"
                    SELECT COUNT(*)
                    FROM contracts
                    WHERE Id = @ContractId AND IsDeleted = 0
                ";

                var contractExists = await connection.ExecuteScalarAsync<int>(
                    contractCheckQuery,
                    new { ContractId = request.ContractId.Value });

                if (contractExists == 0)
                {
                    logger.LogWarning("Contract not found: {ContractId}", request.ContractId.Value);
                    return new CreatePublicHolidayResult
                    {
                        Success = false,
                        ErrorMessage = $"Contract with ID {request.ContractId.Value} not found"
                    };
                }
            }
            
            var dateCheckQuery = @"
                SELECT COUNT(*)
                FROM public_holidays
                WHERE HolidayDate = @HolidayDate
                AND Year = @Year
                AND (ContractId = @ContractId OR (ContractId IS NULL AND @ContractId IS NULL))
            ";

            var dateExists = await connection.ExecuteScalarAsync<int>(
                dateCheckQuery,
                new
                {
                    HolidayDate = request.HolidayDate,
                    Year = request.Year,
                    ContractId = request.ContractId
                });

            if (dateExists > 0)
            {
                logger.LogWarning(
                    "Holiday date {HolidayDate} already exists for year {Year}",
                    request.HolidayDate,
                    request.Year);
                return new CreatePublicHolidayResult
                {
                    Success = false,
                    ErrorMessage = $"A holiday on {request.HolidayDate:yyyy-MM-dd} already exists for year {request.Year}"
                };
            }

            var holidayId = Guid.NewGuid();

            var insertQuery = @"
                INSERT INTO public_holidays (
                    Id, ContractId, HolidayDate, HolidayName, HolidayNameEn,
                    HolidayCategory, IsTetPeriod, IsTetHoliday, TetDayNumber,
                    HolidayStartDate, HolidayEndDate, TotalHolidayDays,
                    IsOfficialHoliday, IsObserved, OriginalDate, ObservedDate,
                    AppliesNationwide, AppliesToRegions,
                    StandardWorkplacesClosed, EssentialServicesOperating,
                    Description, Year, CreatedAt
                ) VALUES (
                    @Id, @ContractId, @HolidayDate, @HolidayName, @HolidayNameEn,
                    @HolidayCategory, @IsTetPeriod, @IsTetHoliday, @TetDayNumber,
                    @HolidayStartDate, @HolidayEndDate, @TotalHolidayDays,
                    @IsOfficialHoliday, @IsObserved, @OriginalDate, @ObservedDate,
                    @AppliesNationwide, @AppliesToRegions,
                    @StandardWorkplacesClosed, @EssentialServicesOperating,
                    @Description, @Year, @CreatedAt
                )
            ";

            var rowsAffected = await connection.ExecuteAsync(insertQuery, new
            {
                Id = holidayId,
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
                CreatedAt = DateTime.UtcNow
            });

            if (rowsAffected == 0)
            {
                logger.LogWarning("No rows affected when creating holiday");
                return new CreatePublicHolidayResult
                {
                    Success = false,
                    ErrorMessage = "Failed to create holiday"
                };
            }

            logger.LogInformation(
                "Successfully created holiday {HolidayName} (ID: {HolidayId})",
                request.HolidayName, holidayId);

            return new CreatePublicHolidayResult
            {
                Success = true,
                HolidayId = holidayId,
                HolidayName = request.HolidayName,
                HolidayDate = request.HolidayDate
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating public holiday: {HolidayName}", request.HolidayName);
            return new CreatePublicHolidayResult
            {
                Success = false,
                ErrorMessage = $"Error creating holiday: {ex.Message}"
            };
        }
    }
}
