namespace Contracts.API.ContractsHandler.GetAllContracts;

public class GetAllContractsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/get-all", async (
            ISender sender,
            [AsParameters] GetAllContractsQueryParams queryParams) =>
        {
            var query = new GetAllContractsQuery
            {
                Status = queryParams.Status,
                ContractType = queryParams.ContractType,
                SearchKeyword = queryParams.SearchKeyword
            };

            var result = await sender.Send(query);

            if (!result.Success)
                return Results.BadRequest(result);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Contracts - Query")
        .WithName("GetAllContracts")
        .Produces<GetAllContractsResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy danh sách tất cả hợp đồng")
        .WithDescription(@"
Lấy danh sách tất cả hợp đồng với các tính năng:
- **Filtering**: Lọc theo status, contractType, searchKeyword
- **Sorting**: Sắp xếp theo CreatedAt giảm dần (hợp đồng mới nhất trước)
- **Days Remaining**: Tự động tính số ngày còn lại đến EndDate
- **Expiry Status**: Trạng thái hết hạn (expired, near_expired, active, etc.)

**Query Parameters:**
- `status`: Lọc theo trạng thái (active, expired, pending, etc.)
- `contractType`: Lọc theo loại hợp đồng (working_contract, service_contract, etc.)
- `searchKeyword`: Tìm kiếm theo tên, email, hoặc mã hợp đồng

**Response includes:**
- Danh sách tất cả contracts với thông tin chi tiết (sắp xếp theo ngày tạo mới nhất)
- Customer name và email
- Start date và end date
- Days remaining (số ngày còn lại)
- Expiry status (trạng thái hết hạn)
- Total count (tổng số hợp đồng)
        ");
    }
}

/// <summary>
/// Query parameters cho GetAllContracts endpoint
/// </summary>
public record GetAllContractsQueryParams
{
    public string? Status { get; init; }
    public string? ContractType { get; init; }
    public string? SearchKeyword { get; init; }
}
