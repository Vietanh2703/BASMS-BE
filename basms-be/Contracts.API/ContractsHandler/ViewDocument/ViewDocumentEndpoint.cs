namespace Contracts.API.ContractsHandler.ViewDocument;

public class ViewDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/documents/{documentId:guid}",
                async (
                    Guid documentId,
                    ISender sender,
                    ILogger<ViewDocumentEndpoint> logger) =>
                {
                    try
                    {
                        logger.LogInformation("Retrieving document {DocumentId}", documentId);

                        var query = new ViewDocumentQuery(DocumentId: documentId);
                        var result = await sender.Send(query);

                        if (!result.Success)
                        {
                            var statusCode = result.ErrorCode switch
                            {
                                "DOCUMENT_NOT_FOUND" => StatusCodes.Status404NotFound,
                                "URL_GENERATION_FAILED" => StatusCodes.Status500InternalServerError,
                                _ => StatusCodes.Status500InternalServerError
                            };

                            logger.LogWarning("Failed to retrieve document: {ErrorCode} - {ErrorMessage}",
                                result.ErrorCode, result.ErrorMessage);

                            return Results.Json(
                                new
                                {
                                    success = false,
                                    error = result.ErrorMessage,
                                    errorCode = result.ErrorCode
                                },
                                statusCode: statusCode
                            );
                        }


                        if (string.IsNullOrEmpty(result.FileUrl))
                        {
                            logger.LogError("File URL is null after successful query");
                            return Results.Problem(
                                title: "Document retrieval failed",
                                detail: "File URL is not available",
                                statusCode: StatusCodes.Status500InternalServerError
                            );
                        }

                        logger.LogInformation(
                            "Successfully generated access URL for document: {DocumentId} - {FileName}",
                            result.DocumentId, result.FileName);
                        
                        return Results.Ok(new
                        {
                            success = true,
                            data = new
                            {
                                fileUrl = result.FileUrl,
                                fileName = result.FileName,
                                contentType = result.ContentType,
                                fileSize = result.FileSize,
                                documentId = result.DocumentId,
                                createdAt = result.CreatedAt,
                                urlExpiresAt = result.UrlExpiresAt
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unexpected error in view document endpoint");
                        return Results.Problem(
                            title: "Document retrieval failed",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status500InternalServerError
                        );
                    }
                })
            .AllowAnonymous()  
            .WithTags("Contracts")
            .WithName("ViewDocument")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Lấy pre-signed URL để xem document (không cần token)");
    }
}
