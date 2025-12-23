namespace Contracts.API.ContractsHandler.CheckExpiredContractById;

public record CheckExpiredContractByIdQuery(Guid ContractId) : IQuery<CheckExpiredContractByIdResult>;

public record CheckExpiredContractByIdResult
{
    public bool Success { get; init; }
    public Guid ContractId { get; init; }
    public string? ContractNumber { get; init; }
    public string? ContractType { get; init; }
    public DateTime? EndDate { get; init; }
    public int? DaysRemaining { get; init; }
    public string? Status { get; init; }
    public string? Message { get; init; }
}

internal class CheckExpiredContractByIdHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<CheckExpiredContractByIdHandler> logger)
    : IQueryHandler<CheckExpiredContractByIdQuery, CheckExpiredContractByIdResult>
{
    public async Task<CheckExpiredContractByIdResult> Handle(
        CheckExpiredContractByIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Checking expired status for contract {ContractId}", request.ContractId);
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nowVietnam = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);

            using var connection = await connectionFactory.CreateConnectionAsync();

            var contract = await connection.QueryFirstOrDefaultAsync<Contract>(@"
                SELECT * FROM contracts
                WHERE Id = @ContractId
                AND IsDeleted = 0",
                new { ContractId = request.ContractId });

            if (contract == null)
            {
                return new CheckExpiredContractByIdResult
                {
                    Success = false,
                    ContractId = request.ContractId,
                    Message = "Không tìm thấy hợp đồng hoặc hợp đồng đã bị xóa"
                };
            }
            
            var document = await connection.QueryFirstOrDefaultAsync<ContractDocument>(@"
                SELECT * FROM contract_documents
                WHERE Id = @DocumentId
                AND IsDeleted = 0",
                new { DocumentId = contract.DocumentId });

            if (document == null || document.EndDate == null)
            {
                return new CheckExpiredContractByIdResult
                {
                    Success = false,
                    ContractId = request.ContractId,
                    ContractNumber = contract.ContractNumber,
                    ContractType = contract.ContractType,
                    Status = contract.Status,
                    Message = "Hợp đồng không có ngày hết hạn"
                };
            }
            
            var endDateVietnam = TimeZoneInfo.ConvertTimeFromUtc(document.EndDate.Value, vietnamTimeZone);
            var daysRemaining = (int)(endDateVietnam - nowVietnam).TotalDays;
            string status;
            string message;

            if (daysRemaining < 0)
            {
                status = "expired";
                message = $"Hợp đồng đã hết hạn {Math.Abs(daysRemaining)} ngày trước";
            }
            else if (daysRemaining == 0)
            {
                status = "expired_today";
                message = "Hợp đồng hết hạn hôm nay";
            }
            else if (daysRemaining <= 7)
            {
                status = "near_expired";
                message = $"Hợp đồng sắp hết hạn trong {daysRemaining} ngày";
            }
            else if (daysRemaining <= 30)
            {
                status = "expiring_soon";
                message = $"Hợp đồng còn {daysRemaining} ngày";
            }
            else
            {
                status = "active";
                message = $"Hợp đồng còn {daysRemaining} ngày";
            }

            logger.LogInformation(
                "Contract {ContractId} check completed: DaysRemaining={Days}, Status={Status}",
                request.ContractId,
                daysRemaining,
                status);

            return new CheckExpiredContractByIdResult
            {
                Success = true,
                ContractId = contract.Id,
                ContractNumber = contract.ContractNumber,
                ContractType = contract.ContractType,
                EndDate = endDateVietnam,
                DaysRemaining = daysRemaining,
                Status = status,
                Message = message
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking expired status for contract {ContractId}", request.ContractId);
            return new CheckExpiredContractByIdResult
            {
                Success = false,
                ContractId = request.ContractId,
                Message = $"Lỗi khi kiểm tra hợp đồng: {ex.Message}"
            };
        }
    }
}
