namespace Contracts.API.ContractsHandler.CheckExpiredContracts;

public class CheckExpiredContractsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/check-expired", async (ISender sender) =>
        {
            var command = new CheckExpiredContractsCommand();
            var result = await sender.Send(command);

            if (!result.Success)
                return Results.BadRequest(result);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Contracts - Background Jobs")
        .WithName("CheckExpiredContracts")
        .Produces<CheckExpiredContractsResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Check and update expired contracts")
        .WithDescription(@"
Background job để check và update trạng thái contracts:
- Near expired: Còn 7 ngày đến EndDate
- Expired: Đã đến EndDate
- Tự động deactivate users (managers, guards, customers) khi contract hết hạn
        ");
    }
}
