namespace Contracts.API.ContractsHandler.UploadBaseContract;

public class UploadBaseContractEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /api/contracts/templates/upload
        app.MapPost("/api/contracts/templates/upload",
            async (HttpRequest request, ISender sender, ILogger<UploadBaseContractEndpoint> logger) =>
            {
                try
                {
                    // Kiểm tra request có file không
                    if (!request.HasFormContentType || request.Form.Files.Count == 0)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "No file uploaded"
                        });
                    }

                    var file = request.Form.Files[0];

                    // Lấy folderPath từ form (optional)
                    var folderPath = request.Form["folderPath"].ToString();
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        folderPath = null; // Để handler tự động detect
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

                    // Tạo command
                    var command = new UploadBaseContractCommand(
                        File: file,
                        Category: category,
                        FolderPath: folderPath
                    );

                    // Gửi command
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
        .DisableAntiforgery() // Disable antiforgery for file upload
        .WithTags("Contracts - Templates")
        .WithName("UploadBaseContractTemplate")
        .Accepts<IFormFile>("multipart/form-data")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Upload base contract template to S3 (keep original filename)")
        .WithDescription(@"
Upload base contract template (Word/PDF) to AWS S3 with original filename preserved.
Template will be used later for auto-filling contracts.

**Form fields:**
- `file` (required): The template file (.docx or .pdf)
- `folderPath` (optional): Custom folder path (e.g., 'templates/service')
  - If not provided, auto-detects based on filename:
    - Files containing 'LAO-DONG' or 'WORKING' → templates/working/
    - Files containing 'DICH-VU' or 'SERVICE' → templates/service/
    - Otherwise → templates/

**Example using curl:**
```bash
curl -X POST http://localhost:5000/api/contracts/templates/upload \
  -F 'file=@HOP-DONG-DICH-VU-BAO-VE.docx'
```

**With custom folder:**
```bash
curl -X POST http://localhost:5000/api/contracts/templates/upload \
  -F 'file=@MY-TEMPLATE.docx' \
  -F 'folderPath=templates/custom'
```

**Validation:**
- Only .docx and .pdf files are allowed
- File name is preserved (no timestamp/GUID added)

**Response example:**
```json
{
  ""success"": true,
  ""data"": {
    ""documentId"": ""abc-123-guid"",
    ""fileUrl"": ""https://bucket.s3.region.amazonaws.com/templates/service/HOP-DONG-DICH-VU.docx"",
    ""fileName"": ""HOP-DONG-DICH-VU.docx"",
    ""fileSize"": 54321
  }
}
```

**Note:** Save the returned `documentId` - use it as `templateDocumentId` when calling `/api/contracts/process-auto`.
");
    }
}
