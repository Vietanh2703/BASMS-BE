namespace Shifts.API.ManagersHandler.CheckTotalGuardSupervised;

public class CheckTotalGuardSupervisedEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shifts/managers/{managerId:guid}/check-guard-count",
                async (Guid managerId, ISender sender) =>
                {
                    var query = new CheckTotalGuardSupervisedQuery(managerId);
                    var result = await sender.Send(query);

                    if (!result.Success)
                    {
                        return Results.NotFound(new
                        {
                            success = false,
                            error = result.ErrorMessage
                        });
                    }

                    return Results.Ok(new
                    {
                        success = true,
                        data = new
                        {
                            managerId = result.ManagerId,
                            managerName = result.ManagerName,
                            actualGuardsCount = result.ActualGuardsCount,
                            totalGuardsSupervised = result.TotalGuardsSupervised,
                            availableSlots = result.AvailableSlots,
                            isOverLimit = result.IsOverLimit,
                            message = result.Message
                        }
                    });
                })
            .RequireAuthorization()
            .WithTags("Managers")
            .WithName("CheckTotalGuardSupervised")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Kiểm tra số guards thực tế so với TotalGuardsSupervised của manager")
            .WithDescription(@"
Kiểm tra số guards thực tế đang được phân công cho manager và so sánh với TotalGuardsSupervised.

**Query:**
- `managerId`: ID của manager cần kiểm tra

**Response:**
```json
{
  ""success"": true,
  ""data"": {
    ""managerId"": ""guid"",
    ""managerName"": ""Nguyen Van A"",
    ""actualGuardsCount"": 15,
    ""totalGuardsSupervised"": 20,
    ""availableSlots"": 5,
    ""isOverLimit"": false,
    ""message"": ""Manager has 15/20 guards. 5 slots available.""
  }
}
```

**Business Logic:**
- `actualGuardsCount`: Đếm số guards có `DirectManagerId = managerId` và `ContractType != 'join_in_request'`
- `totalGuardsSupervised`: Lấy từ `managers.TotalGuardsSupervised`
- `availableSlots`: `totalGuardsSupervised - actualGuardsCount`
- `isOverLimit`: `true` nếu `actualGuardsCount > totalGuardsSupervised`

**Use Cases:**
- Kiểm tra manager còn nhận được bao nhiêu guards nữa
- Phát hiện trường hợp vượt quá giới hạn
- Dashboard thống kê capacity của manager
");
    }
}
