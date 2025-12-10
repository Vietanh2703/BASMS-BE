namespace Contracts.API.ContractsHandler.GetAllContracts;

/// <summary>
/// Query để lấy danh sách tất cả contracts với filtering
/// </summary>
public record GetAllContractsQuery : IQuery<GetAllContractsResult>
{
    public string? Status { get; init; }
    public string? ContractType { get; init; }
    public string? SearchKeyword { get; init; }
}

/// <summary>
/// Result chứa danh sách contracts
/// </summary>
public record GetAllContractsResult
{
    public bool Success { get; init; }
    public List<ContractDto> Contracts { get; init; } = new();
    public int TotalCount { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO cho contract với thông tin chi tiết
/// </summary>
public record ContractDto
{
    public Guid Id { get; init; }
    public string? ContractNumber { get; init; }
    public string ContractType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? DocumentId { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerEmail { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int? DaysRemaining { get; init; }
    public string? ExpiryStatus { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

internal class GetAllContractsHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetAllContractsHandler> logger)
    : IQueryHandler<GetAllContractsQuery, GetAllContractsResult>
{
    public async Task<GetAllContractsResult> Handle(
        GetAllContractsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting all contracts: Status={Status}, Type={Type}",
                request.Status,
                request.ContractType);

            // Get Vietnam timezone (UTC+7)
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nowVietnam = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // Build dynamic WHERE clause
            var whereConditions = new List<string> { "c.IsDeleted = 0" };
            var parameters = new DynamicParameters();

            if (!string.IsNullOrEmpty(request.Status))
            {
                whereConditions.Add("c.Status = @Status");
                parameters.Add("Status", request.Status);
            }

            if (!string.IsNullOrEmpty(request.ContractType))
            {
                whereConditions.Add("c.ContractType = @ContractType");
                parameters.Add("ContractType", request.ContractType);
            }

            if (!string.IsNullOrEmpty(request.SearchKeyword))
            {
                whereConditions.Add(@"(
                    c.ContractNumber LIKE @SearchKeyword
                    OR cust.CompanyName LIKE @SearchKeyword
                    OR cust.ContactPersonName LIKE @SearchKeyword
                    OR cust.Email LIKE @SearchKeyword
                )");
                parameters.Add("SearchKeyword", $"%{request.SearchKeyword}%");
            }

            var whereClause = string.Join(" AND ", whereConditions);

            // Get total count
            var countQuery = $@"
                SELECT COUNT(*)
                FROM contracts c
                LEFT JOIN customers cust ON c.CustomerId = cust.Id
                WHERE {whereClause}";

            var totalCount = await connection.ExecuteScalarAsync<int>(countQuery, parameters);

            if (totalCount == 0)
            {
                return new GetAllContractsResult
                {
                    Success = true,
                    Contracts = new List<ContractDto>(),
                    TotalCount = 0
                };
            }
            
            var query = $@"
                SELECT
                    c.Id,
                    c.ContractNumber,
                    c.ContractType,
                    c.Status,
                    c.DocumentId,
                    c.CustomerId,
                    c.CreatedAt,
                    c.UpdatedAt,
                    COALESCE(cust.ContactPersonName) as CustomerName,
                    cust.Email as CustomerEmail,
                    doc.StartDate,
                    doc.EndDate
                FROM contracts c
                LEFT JOIN customers cust ON c.CustomerId = cust.Id
                LEFT JOIN contract_documents doc ON c.DocumentId = doc.Id AND doc.IsDeleted = 0
                WHERE {whereClause}
                ORDER BY c.CreatedAt DESC";

            var contracts = await connection.QueryAsync<ContractDto>(query, parameters);

            // Calculate days remaining for each contract
            var contractDtos = contracts.Select(c => CalculateDaysRemaining(c, nowVietnam, vietnamTimeZone)).ToList();

            logger.LogInformation(
                "Retrieved {Count} contracts",
                contractDtos.Count);

            return new GetAllContractsResult
            {
                Success = true,
                Contracts = contractDtos,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all contracts");
            return new GetAllContractsResult
            {
                Success = false,
                ErrorMessage = $"Lỗi khi lấy danh sách hợp đồng: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Tính số ngày còn lại và xác định expiry status
    /// </summary>
    private ContractDto CalculateDaysRemaining(ContractDto contract, DateTime nowVietnam, TimeZoneInfo vietnamTimeZone)
    {
        if (contract.EndDate == null)
        {
            return contract with
            {
                DaysRemaining = null,
                ExpiryStatus = "no_end_date"
            };
        }

        // Convert EndDate to Vietnam timezone
        var endDateVietnam = TimeZoneInfo.ConvertTimeFromUtc(contract.EndDate.Value, vietnamTimeZone);
        var daysRemaining = (int)(endDateVietnam - nowVietnam).TotalDays;

        string expiryStatus;
        if (daysRemaining < 0)
        {
            expiryStatus = "expired";
        }
        else if (daysRemaining == 0)
        {
            expiryStatus = "expired_today";
        }
        else if (daysRemaining <= 7)
        {
            expiryStatus = "near_expired";
        }
        else if (daysRemaining <= 30)
        {
            expiryStatus = "expiring_soon";
        }
        else
        {
            expiryStatus = "active";
        }

        return contract with
        {
            EndDate = endDateVietnam,
            DaysRemaining = daysRemaining,
            ExpiryStatus = expiryStatus
        };
    }
}
