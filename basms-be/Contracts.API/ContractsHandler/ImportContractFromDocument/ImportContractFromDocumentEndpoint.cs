namespace Contracts.API.ContractsHandler.ImportContractFromDocument;

public record ImportContractFromDocumentRequest
{
    public Guid DocumentId { get; init; }
}

public class ImportContractFromDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/import-from-document", async (
                [FromBody] ImportContractFromDocumentRequest request,
                ISender sender,
                ILogger<ImportContractFromDocumentEndpoint> logger) =>
            {
                try
                {
                    if (request.DocumentId == Guid.Empty)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            message = "DocumentId is required"
                        });
                    }

                    logger.LogInformation(
                        "Importing contract from DocumentId: {DocumentId} (will extract customer info from document)",
                        request.DocumentId);
                    
                    var command = new ImportContractFromDocumentCommand(
                        DocumentId: request.DocumentId
                    );

                    var result = await sender.Send(command);

                    if (result.Success)
                    {
                        logger.LogInformation(
                            "Successfully imported contract {ContractId} from document {DocumentId}",
                            result.ContractId,
                            request.DocumentId);

                        return Results.Ok(result);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Failed to import contract from document {DocumentId}: {ErrorMessage}",
                            request.DocumentId,
                            result.ErrorMessage);

                        return Results.BadRequest(result);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error importing contract from document");
                    return Results.Problem(
                        title: "Error importing contract",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            })
            .RequireAuthorization()
            .WithTags("Contracts")
            .WithName("ImportContractFromDocument")
            .Accepts<ImportContractFromDocumentRequest>("application/json")
            .Produces<ImportContractFromDocumentResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Import contract từ document đã upload trên S3");


    }
}
