namespace Contracts.API.ContractsHandler.GetDocumentById;

public class GetDocumentByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/documents/details/{documentId:guid}", async (
            Guid documentId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetDocumentByIdQuery(documentId);
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
                data = result.Document
            });
        })
        .RequireAuthorization()
        .WithName("GetDocumentById")
        .WithTags("Contracts - Documents")
        .WithSummary("Lấy thông tin chi tiết document theo ID")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces<object>(StatusCodes.Status404NotFound);
    }
}
