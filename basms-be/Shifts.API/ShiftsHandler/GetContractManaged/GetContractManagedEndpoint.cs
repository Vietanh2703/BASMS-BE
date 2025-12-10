using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Shifts.API.ShiftsHandler.GetContractManaged;

/// <summary>
/// Endpoint để lấy danh sách contracts UNIQUE mà manager phụ trách
/// </summary>
public class GetContractManagedEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/contracts/managed/{managerId:guid}", async (
            [FromRoute] Guid managerId,
            [FromQuery] string? status,
            ISender sender,
            ILogger<GetContractManagedEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/shifts/contracts/managed/{ManagerId} - Getting unique contracts managed",
                managerId);

            var query = new GetContractManagedQuery(
                ManagerId: managerId,
                Status: status
            );

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to get contracts for Manager {ManagerId}: {Error}",
                    managerId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                "✓ Found {Count} unique contracts for Manager {ManagerId}",
                result.TotalCount,
                managerId);

            return Results.Ok(new
            {
                success = true,
                data = result.Contracts,
                totalCount = result.TotalCount,
                filters = new
                {
                    managerId,
                    status = status ?? "all"
                }
            });
        })
        .RequireAuthorization()
        .WithName("GetContractManaged")
        .WithTags("Contracts - Manager")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .WithSummary("Lấy danh sách contracts UNIQUE mà manager phụ trách")
        .WithDescription(@"
**Chức năng:**
Trả về danh sách contracts UNIQUE mà manager phụ trách (tránh duplicate do nhiều shift templates cùng contractId).

**Giải pháp:**
- Sử dụng GROUP BY ContractId để loại bỏ duplicate
- Mỗi contract chỉ hiển thị 1 lần duy nhất
- Tổng hợp (aggregate) thông tin từ các shift templates:
  - TotalShiftTemplates: Tổng số shift templates của contract
  - TotalActiveTemplates: Số shift templates đang active
  - LocationInfo: Lấy từ template đầu tiên (theo CreatedAt)

**Query Parameters:**
- `status` (optional): Lọc theo status của shift templates (await_create_shift, active, inactive, archived)

**Use Case:**
- Manager xem danh sách contracts mình phụ trách (không bị duplicate)
- Mỗi contract có thể có nhiều shift templates (ca sáng, ca chiều, ca tối) nhưng chỉ hiện 1 lần
- Biết được mỗi contract có bao nhiêu shift templates

**Response Example:**
```json
{
  ""success"": true,
  ""data"": [
    {
      ""contractId"": ""123e4567-e89b-12d3-a456-426614174000"",
      ""managerId"": ""987e4567-e89b-12d3-a456-426614174000"",
      ""locationId"": ""abc-123"",
      ""locationName"": ""Bệnh viện ABC"",
      ""locationAddress"": ""123 Đường XYZ, Q.1, TP.HCM"",
      ""totalShiftTemplates"": 3,
      ""totalActiveTemplates"": 3,
      ""status"": ""await_create_shift"",
      ""effectiveFrom"": ""2025-01-01T00:00:00Z"",
      ""effectiveTo"": ""2025-12-31T23:59:59Z"",
      ""earliestCreatedAt"": ""2025-01-01T08:00:00Z"",
      ""latestUpdatedAt"": ""2025-01-02T10:30:00Z""
    }
  ],
  ""totalCount"": 1
}
```

**Examples:**
```
GET /api/shifts/contracts/managed/{managerId}
GET /api/shifts/contracts/managed/{managerId}?status=await_create_shift
GET /api/shifts/contracts/managed/{managerId}?status=active
```
        ");
    }
}
