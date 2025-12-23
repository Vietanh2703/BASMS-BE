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
        .Produces<object>(StatusCodes.Status200OK)
        .Produces<object>(StatusCodes.Status404NotFound);
    }
}
