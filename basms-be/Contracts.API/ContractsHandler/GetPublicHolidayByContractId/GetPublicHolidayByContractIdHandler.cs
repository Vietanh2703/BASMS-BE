namespace Contracts.API.ContractsHandler.GetPublicHolidayByContractId;

public record GetPublicHolidayByContractIdQuery(Guid ContractId) : IQuery<GetPublicHolidayByContractIdResult>;

public record PublicHolidayDto
{
    public Guid Id { get; init; }
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

public record GetPublicHolidayByContractIdResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? ContractId { get; init; }
    public string? ContractCode { get; init; }
    public List<PublicHolidayDto> PublicHolidays { get; init; } = new();
}

internal class GetPublicHolidayByContractIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetPublicHolidayByContractIdHandler> logger)
    : IQueryHandler<GetPublicHolidayByContractIdQuery, GetPublicHolidayByContractIdResult>
{
    public async Task<GetPublicHolidayByContractIdResult> Handle(
        GetPublicHolidayByContractIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting public holidays for contract: {ContractId}", request.ContractId);
            using var connection = await connectionFactory.CreateConnectionAsync();
            var contractQuery = @"
                SELECT ContractNumber
                FROM contracts
                WHERE Id = @ContractId AND IsDeleted = 0
            ";

            var contractNumber = await connection.QuerySingleOrDefaultAsync<string>(
                contractQuery,
                new { request.ContractId });

            if (contractNumber == null)
            {
                logger.LogWarning("Contract not found: {ContractId}", request.ContractId);
                return new GetPublicHolidayByContractIdResult
                {
                    Success = false,
                    ErrorMessage = $"Contract with ID {request.ContractId} not found"
                };
            }
            
            var publicHolidaysQuery = @"
                SELECT
                    Id, ContractId, HolidayDate, HolidayName, HolidayNameEn,
                    HolidayCategory, IsTetPeriod, IsTetHoliday, TetDayNumber,
                    HolidayStartDate, HolidayEndDate, TotalHolidayDays,
                    IsOfficialHoliday, IsObserved, OriginalDate, ObservedDate,
                    AppliesNationwide, AppliesToRegions,
                    StandardWorkplacesClosed, EssentialServicesOperating,
                    Description, Year
                FROM public_holidays
                WHERE (ContractId = @ContractId OR ContractId IS NULL)
                AND Year >= YEAR(NOW())
                ORDER BY HolidayDate
            ";

            var publicHolidays = await connection.QueryAsync<PublicHolidayDto>(
                publicHolidaysQuery,
                new { request.ContractId });

            var publicHolidaysList = publicHolidays.ToList();

            logger.LogInformation(
                "Successfully retrieved {Count} public holiday(s) for contract {ContractNumber}",
                publicHolidaysList.Count, contractNumber);

            return new GetPublicHolidayByContractIdResult
            {
                Success = true,
                ContractId = request.ContractId,
                ContractCode = contractNumber,
                PublicHolidays = publicHolidaysList
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting public holidays for contract: {ContractId}", request.ContractId);
            return new GetPublicHolidayByContractIdResult
            {
                Success = false,
                ErrorMessage = $"Error getting public holidays: {ex.Message}"
            };
        }
    }
}
