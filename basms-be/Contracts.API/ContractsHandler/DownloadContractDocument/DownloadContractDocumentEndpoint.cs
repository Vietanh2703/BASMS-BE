namespace Contracts.API.ContractsHandler.DownloadContractDocument;


public class DownloadContractDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/documents/{documentId:guid}/download", async (
            Guid documentId,
            ISender sender,
            ILogger<DownloadContractDocumentEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Download request for document: {DocumentId}", documentId);

                var query = new DownloadContractDocumentQuery(documentId);
                var result = await sender.Send(query);

                if (!result.Success || result.FileStream == null)
                {
                    logger.LogWarning(
                        "Download failed for document {DocumentId}: {ErrorMessage}",
                        documentId,
                        result.ErrorMessage);

                    return Results.NotFound(new
                    {
                        success = false,
                        message = result.ErrorMessage ?? "Document not found or download failed"
                    });
                }

                logger.LogInformation(
                    "Successfully prepared download for: {FileName}",
                    result.FileName);
                
                return Results.File(
                    fileStream: result.FileStream,
                    contentType: result.ContentType ?? "application/octet-stream",
                    fileDownloadName: result.FileName,
                    enableRangeProcessing: true
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing download request for document: {DocumentId}", documentId);
                return Results.Problem(
                    title: "Error downloading document",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Documents")
        .WithName("DownloadContractDocument")
        .Produces<FileStreamResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Download contract document từ AWS S3");
    }
}
