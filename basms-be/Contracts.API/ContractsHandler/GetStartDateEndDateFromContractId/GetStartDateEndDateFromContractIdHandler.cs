namespace Contracts.API.ContractsHandler.GetStartDateEndDateFromContractId;

public record GetStartDateEndDateFromContractIdQuery(Guid ContractId)
    : IQuery<GetStartDateEndDateFromContractIdResult>;

public record GetStartDateEndDateFromContractIdResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

internal class GetStartDateEndDateFromContractIdHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetStartDateEndDateFromContractIdHandler> logger)
    : IQueryHandler<GetStartDateEndDateFromContractIdQuery, GetStartDateEndDateFromContractIdResult>
{
    public async Task<GetStartDateEndDateFromContractIdResult> Handle(
        GetStartDateEndDateFromContractIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting StartDate and EndDate for ContractId: {ContractId}", request.ContractId);

            using var connection = await dbFactory.CreateConnectionAsync();

            var sql = @"
                SELECT StartDate, EndDate
                FROM contracts
                WHERE Id = @ContractId
                  AND IsDeleted = 0";

            var result = await connection.QueryFirstOrDefaultAsync<(DateTime StartDate, DateTime EndDate)?>(
                sql,
                new { request.ContractId });

            if (result == null)
            {
                logger.LogWarning("Contract not found: {ContractId}", request.ContractId);
                return new GetStartDateEndDateFromContractIdResult
                {
                    Success = false,
                    ErrorMessage = "Contract not found",
                    StartDate = null,
                    EndDate = null
                };
            }

            logger.LogInformation(
                "Found contract dates: StartDate={StartDate:yyyy-MM-dd}, EndDate={EndDate:yyyy-MM-dd}",
                result.Value.StartDate,
                result.Value.EndDate);

            return new GetStartDateEndDateFromContractIdResult
            {
                Success = true,
                ErrorMessage = null,
                StartDate = result.Value.StartDate,
                EndDate = result.Value.EndDate
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting contract dates for ContractId: {ContractId}", request.ContractId);
            return new GetStartDateEndDateFromContractIdResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                StartDate = null,
                EndDate = null
            };
        }
    }
}
