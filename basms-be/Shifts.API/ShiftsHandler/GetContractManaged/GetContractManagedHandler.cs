using Dapper;
using Shifts.API.Data;

namespace Shifts.API.ShiftsHandler.GetContractManaged;

/// <summary>
/// Query để lấy danh sách contracts UNIQUE mà manager phụ trách
/// </summary>
public record GetContractManagedQuery(
    Guid ManagerId,
    string? Status = null
) : IQuery<GetContractManagedResult>;

/// <summary>
/// Result chứa danh sách contracts unique
/// </summary>
public record GetContractManagedResult
{
    public bool Success { get; init; }
    public List<ContractManagedDto> Contracts { get; init; } = new();
    public int TotalCount { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO cho contract managed (unique per contract)
/// </summary>
public record ContractManagedDto
{
    public Guid? ContractId { get; init; }
    public Guid? ManagerId { get; init; }

    // Location info (from first template)
    public Guid? LocationId { get; init; }
    public string? LocationName { get; init; }
    public string? LocationAddress { get; init; }
    public decimal? LocationLatitude { get; init; }
    public decimal? LocationLongitude { get; init; }

    // Aggregated info (from shift_templates)
    public int TotalShiftTemplates { get; init; }
    public int TotalActiveTemplates { get; init; }
    public string? TemplateStatus { get; init; }

    // Date info
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public DateTime? EarliestCreatedAt { get; init; }
    public DateTime? LatestUpdatedAt { get; init; }
}

/// <summary>
/// Handler để lấy danh sách contracts UNIQUE theo manager
/// Sử dụng GROUP BY để tránh duplicate contracts (vì 1 contract có nhiều shift templates)
/// </summary>
internal class GetContractManagedHandler(
    IDbConnectionFactory dbFactory,
    ILogger<GetContractManagedHandler> logger)
    : IQueryHandler<GetContractManagedQuery, GetContractManagedResult>
{
    public async Task<GetContractManagedResult> Handle(
        GetContractManagedQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Getting unique contracts managed by Manager {ManagerId} with Status={Status}",
                request.ManagerId,
                request.Status ?? "ALL");

            using var connection = await dbFactory.CreateConnectionAsync();

            // ================================================================
            // BUILD DYNAMIC WHERE CLAUSE
            // ================================================================
            var whereClauses = new List<string>
            {
                "st.ManagerId = @ManagerId",
                "st.IsDeleted = 0",
                "st.ContractId IS NOT NULL"  // Chỉ lấy templates có ContractId
            };

            var parameters = new DynamicParameters();
            parameters.Add("ManagerId", request.ManagerId);

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                whereClauses.Add("st.Status = @Status");
                parameters.Add("Status", request.Status);
            }

            var whereClause = string.Join(" AND ", whereClauses);

            // ================================================================
            // QUERY WITH GROUP BY - LẤY UNIQUE CONTRACTS
            // ================================================================
            var sql = $@"
                SELECT
                    st.ContractId,
                    st.ManagerId,

                    -- Location info (từ shift_templates)
                    MIN(st.LocationId) as LocationId,
                    MIN(st.LocationName) as LocationName,
                    MIN(st.LocationAddress) as LocationAddress,
                    MIN(st.LocationLatitude) as LocationLatitude,
                    MIN(st.LocationLongitude) as LocationLongitude,

                    -- Aggregated data (từ shift_templates)
                    COUNT(*) as TotalShiftTemplates,
                    SUM(CASE WHEN st.IsActive = 1 THEN 1 ELSE 0 END) as TotalActiveTemplates,
                    MIN(st.Status) as TemplateStatus,

                    -- Date info
                    MIN(st.EffectiveFrom) as EffectiveFrom,
                    MAX(st.EffectiveTo) as EffectiveTo,
                    MIN(st.CreatedAt) as EarliestCreatedAt,
                    MAX(st.UpdatedAt) as LatestUpdatedAt
                FROM shift_templates st
                WHERE {whereClause}
                GROUP BY st.ContractId, st.ManagerId
                ORDER BY EarliestCreatedAt DESC";

            var contracts = await connection.QueryAsync<ContractManagedDto>(sql, parameters);
            var contractsList = contracts.ToList();

            logger.LogInformation(
                "Found {Count} unique contracts for Manager {ManagerId}",
                contractsList.Count,
                request.ManagerId);

            return new GetContractManagedResult
            {
                Success = true,
                Contracts = contractsList,
                TotalCount = contractsList.Count
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting contracts managed by Manager {ManagerId}",
                request.ManagerId);

            return new GetContractManagedResult
            {
                Success = false,
                Contracts = new List<ContractManagedDto>(),
                TotalCount = 0,
                ErrorMessage = $"Failed to get managed contracts: {ex.Message}"
            };
        }
    }
}
