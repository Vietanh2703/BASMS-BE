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
        .RequireAuthorization()
        .WithName("GetContractById")
        .WithTags("Contracts")
        .WithSummary("Lấy thông tin chi tiết contract theo ID")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces<object>(StatusCodes.Status404NotFound);
    }
}
