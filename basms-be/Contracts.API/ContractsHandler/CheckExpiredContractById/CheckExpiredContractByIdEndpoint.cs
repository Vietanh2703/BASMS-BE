namespace Contracts.API.ContractsHandler.CheckExpiredContractById;

public class CheckExpiredContractByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/{contractId:guid}/check-expired",
            async (Guid contractId, ISender sender) =>
        {
            var query = new CheckExpiredContractByIdQuery(contractId);
            var result = await sender.Send(query);

            if (!result.Success)
                return Results.BadRequest(result);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Contracts - Status Check")
        .WithName("CheckExpiredContractById")
        .Produces<CheckExpiredContractByIdResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Kiểm tra số ngày còn lại của hợp đồng")
        .WithDescription(@"
Endpoint để kiểm tra trạng thái và số ngày còn lại của một hợp đồng cụ thể:
- Tính số ngày từ hôm nay đến EndDate (timezone Vietnam UTC+7)
- Số ngày dương: Còn thời gian
- Số ngày âm: Đã quá hạn
- Trả về status: expired, expired_today, near_expired, expiring_soon, active
        ");
    }
}
