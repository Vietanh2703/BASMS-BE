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
        .WithSummary("Kiểm tra số ngày còn lại của hợp đồng");
    }
}
