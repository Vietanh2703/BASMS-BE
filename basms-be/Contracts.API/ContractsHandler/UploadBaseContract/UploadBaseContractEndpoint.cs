namespace Contracts.API.ContractsHandler.UploadBaseContract;

public class UploadBaseContractEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/templates/upload",
            async (HttpRequest request, ISender sender, ILogger<UploadBaseContractEndpoint> logger) =>
            {
                try
                {
                    if (!request.HasFormContentType || request.Form.Files.Count == 0)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "No file uploaded"
                        });
                    }

                    var file = request.Form.Files[0];


                    var folderPath = request.Form["folderPath"].ToString();
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        folderPath = null;
                    }
                    
                    var category = request.Form["category"].FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(category))
                    {
                        category = null;
                    }
                    else
                    {
                        category = category.Trim();
                    }
                    
                    var command = new UploadBaseContractCommand(
                        File: file,
                        Category: category,
                        FolderPath: folderPath
                    );
                    
                    var result = await sender.Send(command);

                    if (!result.Success)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = result.ErrorMessage
                        });
                    }

                    return Results.Ok(new
                    {
                        success = true,
                        data = new
                        {
                            documentId = result.DocumentId,
                            category = result.Category,
                            fileUrl = result.FileUrl,
                            fileName = result.FileName,
                            fileSize = result.FileSize
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in upload base contract template endpoint");
                    return Results.Problem(
                        title: "Upload template failed",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            })
        .DisableAntiforgery() 
        .WithTags("Contracts - Templates")
        .WithName("UploadBaseContractTemplate")
        .Accepts<IFormFile>("multipart/form-data")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Upload base contract template to S3 (keep original filename)");
    }
}
