namespace Contracts.API.ContractsHandler.GetPublicHolidayByContractId;

// ================================================================
// QUERY & RESULT
// ================================================================

/// <summary>
/// Query để lấy danh sách public holidays theo contract ID
/// </summary>
public record GetPublicHolidayByContractIdQuery(Guid ContractId) : IQuery<GetPublicHolidayByContractIdResult>;

/// <summary>
/// DTO cho Public Holiday info
/// </summary>
public record PublicHolidayDto
{
    public Guid Id { get; init; }
    public Guid? ContractId { get; init; }
    public DateTime HolidayDate { get; init; }
    public string HolidayName { get; init; } = string.Empty;
    public string? HolidayNameEn { get; init; }
    public string HolidayCategory { get; init; } = string.Empty;

    // Tết Special
    public bool IsTetPeriod { get; init; }
    public bool IsTetHoliday { get; init; }
    public int? TetDayNumber { get; init; }
    public DateTime? HolidayStartDate { get; init; }
    public DateTime? HolidayEndDate { get; init; }
    public int? TotalHolidayDays { get; init; }

    // Quy định nghỉ
    public bool IsOfficialHoliday { get; init; }
    public bool IsObserved { get; init; }
    public DateTime? OriginalDate { get; init; }
    public DateTime? ObservedDate { get; init; }

    // Phạm vi áp dụng
    public bool AppliesNationwide { get; init; }
    public string? AppliesToRegions { get; init; }

    // Ảnh hưởng công việc
    public bool StandardWorkplacesClosed { get; init; }
    public bool EssentialServicesOperating { get; init; }
    public string? Description { get; init; }
    public int Year { get; init; }
}

/// <summary>
/// Kết quả query
/// </summary>
public record GetPublicHolidayByContractIdResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? ContractId { get; init; }
    public string? ContractCode { get; init; }
    public List<PublicHolidayDto> PublicHolidays { get; init; } = new();
}

// ================================================================
// HANDLER
// ================================================================

/// <summary>
/// Handler để lấy danh sách public holidays theo contract ID
/// </summary>
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

            // ================================================================
            // 1. CHECK IF CONTRACT EXISTS
            // ================================================================
            var contractQuery = @"
                SELECT ContractNumber
                FROM contracts
                WHERE Id = @ContractId AND IsDeleted = 0
            ";

            var contractNumber = await connection.QuerySingleOrDefaultAsync<string>(
                contractQuery,
                new { ContractId = request.ContractId });

            if (contractNumber == null)
            {
                logger.LogWarning("Contract not found: {ContractId}", request.ContractId);
                return new GetPublicHolidayByContractIdResult
                {
                    Success = false,
                    ErrorMessage = $"Contract with ID {request.ContractId} not found"
                };
            }

            // ================================================================
            // 2. GET PUBLIC HOLIDAYS
            // ================================================================
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
                new { ContractId = request.ContractId });

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
