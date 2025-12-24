namespace Contracts.API.ContractsHandler.ViewDocumentByToken;

public class ViewDocumentByTokenEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/documents/{documentId:guid}/view",
                async (
                    Guid documentId,
                    string? token,
                    ISender sender,
                    ILogger<ViewDocumentByTokenEndpoint> logger) =>
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(token))
                        {
                            logger.LogWarning("Token parameter is missing");
                            return Results.BadRequest(new
                            {
                                success = false,
                                error = "Security token is required",
                                errorCode = "TOKEN_REQUIRED"
                            });
                        }

                        logger.LogInformation("Retrieving document {DocumentId} with token", documentId);

                        var query = new ViewDocumentByTokenQuery(
                            DocumentId: documentId,
                            Token: token);
                        var result = await sender.Send(query);

                        if (!result.Success)
                        {
                            var statusCode = result.ErrorCode switch
                            {
                                "TOKEN_REQUIRED" or "INVALID_TOKEN" => StatusCodes.Status401Unauthorized,
                                "TOKEN_EXPIRED" => StatusCodes.Status401Unauthorized,
                                "DOWNLOAD_FAILED" => StatusCodes.Status404NotFound,
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
            .WithName("ViewDocumentByToken")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Lấy pre-signed URL để xem document (dùng cho ký điện tử)");
    }
}
