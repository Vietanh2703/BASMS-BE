using BuildingBlocks.Messaging.Events;
using Dapper;

namespace Contracts.API.Consumers;

/// <summary>
/// Consumer trả về thông tin contract
/// Used by: Shifts.API background job để lấy contract info
/// </summary>
public class GetContractConsumer : IConsumer<GetContractRequest>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<GetContractConsumer> _logger;

    public GetContractConsumer(
        IDbConnectionFactory dbFactory,
        ILogger<GetContractConsumer> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetContractRequest> context)
    {
        var request = context.Message;

        _logger.LogInformation(
            "Received GetContractRequest for ContractId: {ContractId}",
            request.ContractId);

        try
        {
            using var connection = await _dbFactory.CreateConnectionAsync();

            var contract = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT
                    Id,
                    CustomerId,
                    ContractNumber,
                    ContractTitle,
                    StartDate,
                    EndDate,
                    Status,
                    IsActive,
                    AutoGenerateShifts,
                    GenerateShiftsAdvanceDays,
                    CreatedBy
                FROM contracts
                WHERE Id = @ContractId
                  AND IsDeleted = 0",
                new { request.ContractId });

            if (contract == null)
            {
                _logger.LogWarning(
                    "Contract {ContractId} not found",
                    request.ContractId);

                // Return error response
                await context.RespondAsync(new GetContractResponse
                {
                    Success = false,
                    Contract = null,
                    ErrorMessage = "Contract không tồn tại hoặc đã bị xóa"
                });
                return;
            }

            var response = new GetContractResponse
            {
                Success = true,
                Contract = new ContractData
                {
                    Id = (Guid)contract.Id,
                    CustomerId = contract.CustomerId ?? Guid.Empty,
                    ContractNumber = contract.ContractNumber,
                    ContractTitle = contract.ContractTitle ?? string.Empty,
                    StartDate = (DateTime)contract.StartDate,
                    EndDate = (DateTime)contract.EndDate,
                    Status = contract.Status ?? "draft",
                    IsActive = contract.IsActive == 1,
                    AutoGenerateShifts = contract.AutoGenerateShifts == 1,
                    GenerateShiftsAdvanceDays = contract.GenerateShiftsAdvanceDays ?? 30,
                    CreatedBy = contract.CreatedBy
                },
                ErrorMessage = null
            };

            _logger.LogInformation(
                "Returning contract info for {ContractNumber}",
                response.Contract.ContractNumber);

            await context.RespondAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get contract info for ContractId: {ContractId}",
                request.ContractId);

            throw; // Re-throw to trigger MassTransit retry
        }
    }
}
