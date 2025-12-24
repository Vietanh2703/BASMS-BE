namespace Contracts.API.ContractsHandler.GetAllContractDocuments;

public class GetAllContractDocumentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/documents", async (
            ISender sender,
            ILogger<GetAllContractDocumentsEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get all contract documents request");

                var query = new GetAllContractDocumentsQuery();
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogError("Failed to get documents: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error getting documents",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }

                logger.LogInformation(
                    "Successfully retrieved {Count} contract documents",
                    result.TotalCount);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get all documents request");
                return Results.Problem(
                    title: "Error getting documents",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithTags("Contracts - Documents")
        .WithName("GetAllContractDocuments")
        .Produces<GetAllContractDocumentsResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy tất cả contract documents từ AWS S3");
    }
}