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
        .WithSummary("Lấy danh sách tất cả hợp đồng");
    }
}

public record GetAllContractsQueryParams
{
    public string? Status { get; init; }
    public string? ContractType { get; init; }
    public string? SearchKeyword { get; init; }
}
