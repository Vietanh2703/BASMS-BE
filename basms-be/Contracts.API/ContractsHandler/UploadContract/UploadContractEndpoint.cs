namespace Contracts.API.ContractsHandler.UploadContract;

public class UploadContractEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /api/contracts/documents/upload
        app.MapPost("/api/contracts/documents/upload",
            async (HttpRequest request, ISender sender, ILogger<UploadContractEndpoint> logger) =>
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

                    // Lấy thông tin từ form
                    var documentType = request.Form["documentType"].ToString();
                    if (string.IsNullOrEmpty(documentType))
                    {
                        documentType = "contract"; // Default
                    }

                    var category = request.Form["category"].ToString();
                    if (string.IsNullOrWhiteSpace(category))
                    {
                        category = null;
                    }

                    DateTime? documentDate = null;
                    var documentDateStr = request.Form["documentDate"].ToString();
                    if (!string.IsNullOrEmpty(documentDateStr) && DateTime.TryParse(documentDateStr, out var parsedDate))
                    {
                        documentDate = parsedDate;
                    }

                    // Lấy UploadedBy từ claims (JWT token)
                    var uploadedByStr = request.Form["uploadedBy"].ToString();
                    if (string.IsNullOrEmpty(uploadedByStr) || !Guid.TryParse(uploadedByStr, out var uploadedBy))
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "UploadedBy user ID is required"
                        });
                    }

                    // Tạo command
                    using var fileStream = file.OpenReadStream();
                    var command = new UploadContractCommand(
                        FileStream: fileStream,
                        FileName: file.FileName,
                        ContentType: file.ContentType,
                        FileSize: file.Length,
                        DocumentType: documentType,
                        Category: category,
                        DocumentDate: documentDate,
                        UploadedBy: uploadedBy
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
                            fileUrl = result.FileUrl,
                            documentName = result.DocumentName,
                            fileSize = result.FileSize,
                            documentType = result.DocumentType,
                            category = result.Category
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in upload contract endpoint");
                    return Results.Problem(
                        title: "Upload failed",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            })
        .DisableAntiforgery() // Disable antiforgery for file upload
        .WithTags("Contracts")
        .WithName("UploadContract")
        .Accepts<IFormFile>("multipart/form-data")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Upload contract document to S3")
        .WithDescription(@"
Upload contract document (Word/PDF, max 10MB) to AWS S3.
Document is created independently first, then contract will reference it later via DocumentId.

**Form fields:**
- `file` (required): The document file (.pdf, .doc, .docx)
- `documentType` (optional): Type of document - contract, amendment, appendix, requirements, site_plan (default: contract)
- `category` (optional): Category of contract - labor_contract, service_contract, working_contract, etc.
- `documentDate` (optional): Date of the document (ISO format: yyyy-MM-dd)
- `uploadedBy` (required): GUID of the user uploading the document

**Example using curl:**
```bash
curl -X POST http://localhost:5000/api/contracts/documents/upload \
  -F 'file=@contract.pdf' \
  -F 'documentType=contract' \
  -F 'category=labor_contract' \
  -F 'documentDate=2024-01-15' \
  -F 'uploadedBy=550e8400-e29b-41d4-a716-446655440000'
```

**Category Examples:**
- `labor_contract`: Hợp đồng lao động
- `service_contract`: Hợp đồng dịch vụ
- `working_contract`: Hợp đồng làm việc
- `internship_contract`: Hợp đồng thực tập
- `probation_contract`: Hợp đồng thử việc

**Validation:**
- File size must be ≤ 10MB
- Only PDF and Word documents are allowed
- Content type must match file extension

**Note:** Save the returned `documentId` - you'll need it when importing the contract.
");
    }
}