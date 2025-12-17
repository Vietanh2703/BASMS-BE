using Carter;
using MediatR;

namespace Contracts.API.ContractsHandler.GetStartDateEndDateFromContractId;

public class GetStartDateEndDateFromContractIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/{contractId:guid}/dates", async (
            Guid contractId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetStartDateEndDateFromContractIdQuery(contractId);
            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                return Results.NotFound(new
                {
                    success = false,
                    message = result.ErrorMessage
                });
            }

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    startDate = result.StartDate?.ToString("yyyy-MM-dd"),
                    endDate = result.EndDate?.ToString("yyyy-MM-dd")
                }
            });
        })
        .RequireAuthorization()
        .WithName("GetStartDateEndDateFromContractId")
        .WithTags("Contracts")
        .WithSummary("Lấy StartDate và EndDate của contract")
        .WithDescription(@"
Lấy ngày bắt đầu và ngày kết thúc của hợp đồng theo ContractId.

## Use case:
- Hiển thị thời hạn hợp đồng
- Validate date range khi tạo shifts
- Kiểm tra hợp đồng còn hiệu lực

## Response:
```json
{
  ""success"": true,
  ""data"": {
    ""startDate"": ""2025-01-01"",
    ""endDate"": ""2025-12-31""
  }
}
```

## Error Response (Contract not found):
```json
{
  ""success"": false,
  ""message"": ""Contract not found""
}
```
")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces<object>(StatusCodes.Status404NotFound);
    }
}
