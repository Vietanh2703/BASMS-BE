namespace Contracts.API.ContractsHandler.GetContractById;

public class GetContractByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetContractByIdQuery(id);
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
                data = result.Contract
            });
        })
        .WithName("GetContractById")
        .WithTags("Contracts")
        .WithSummary("Lấy thông tin chi tiết contract theo ID")
        .WithDescription(@"
Lấy thông tin đầy đủ của contract bao gồm:
- Contract details (thông tin hợp đồng)
- Customer info (thông tin khách hàng)
- Contract documents (tài liệu hợp đồng)
- Contract locations (địa điểm trong hợp đồng + chi tiết customer location)
- Shift schedules (lịch ca trực)
- Contract periods (các kỳ hợp đồng - ban đầu và gia hạn)
- Working conditions (điều kiện làm việc: tăng ca, ca đêm, Tết, phụ cấp...)

## Response Structure:
```json
{
  ""success"": true,
  ""data"": {
    ""id"": ""guid"",
    ""contractNumber"": ""CTR-2025-001"",
    ""contractTitle"": ""Hợp đồng bảo vệ 24/7"",
    ""customer"": {
      ""companyName"": ""Công ty ABC"",
      ""contactPersonName"": ""Nguyễn Văn A"",
      ...
    },
    ""documents"": [...],
    ""locations"": [
      {
        ""guardsRequired"": 2,
        ""coverageType"": ""24x7"",
        ""locationDetails"": {
          ""locationName"": ""Chi nhánh Quận 1"",
          ""address"": ""123 Nguyễn Huệ"",
          ""latitude"": 10.762622,
          ""longitude"": 106.660172,
          ...
        }
      }
    ],
    ""shiftSchedules"": [...],
    ""periods"": [...],
    ""workingConditions"": {
      ""overtimeRateWeekday"": 1.5,
      ""nightShiftRate"": 1.3,
      ""tetHolidayRate"": 4.0,
      ...
    }
  }
}
```
")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces<object>(StatusCodes.Status404NotFound);
    }
}
