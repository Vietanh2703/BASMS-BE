namespace Contracts.API.ContractsHandler.SignContract;

public class SignContractFromDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/sign-document",
            async (HttpRequest request,
                ISender sender,
                ILogger<SignContractFromDocumentEndpoint> logger) =>
            {
                try
                {
                    // ================================================================
                    // PARSE MULTIPART/FORM-DATA REQUEST
                    // ================================================================
                    if (!request.HasFormContentType)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "Request must be multipart/form-data"
                        });
                    }

                    var form = await request.ReadFormAsync();

                    // Required: documentId
                    var documentIdStr = form["documentId"].ToString();
                    if (string.IsNullOrWhiteSpace(documentIdStr))
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "documentId is required"
                        });
                    }

                    if (!Guid.TryParse(documentIdStr, out var documentId))
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "Invalid documentId format"
                        });
                    }

                    // Required: Signature image file
                    if (form.Files.Count == 0)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "Signature image file is required"
                        });
                    }

                    var signatureImage = form.Files[0];

                    // Validate file is an image
                    var allowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
                    var fileExtension = Path.GetExtension(signatureImage.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "Signature image must be PNG or JPG format"
                        });
                    }

                    logger.LogInformation(
                        "Inserting signature - DocumentId: {DocumentId}, Image: {FileName} ({Size} bytes)",
                        documentId, signatureImage.FileName, signatureImage.Length);

                    // ================================================================
                    // TẠO COMMAND VÀ GỬI ĐẾN HANDLER
                    // ================================================================
                    var command = new SignContractFromDocumentCommand(
                        DocumentId: documentId,
                        SignatureImage: signatureImage
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
                            fileUrl = result.FileUrl,
                            fileName = result.FileName
                        },
                        message = "Signature inserted successfully"
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error inserting signature");
                    return Results.Problem(
                        title: "Insert signature failed",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            })
            .DisableAntiforgery()
            .AllowAnonymous()
            .WithTags("Contracts")
            .WithName("InsertSignatureToDocument")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Chèn ảnh chữ ký vào hợp đồng")
            .WithDescription(@"
## Mô tả
Chèn ảnh chữ ký vào vị trí Content Control với tag 'DigitalSignature' trong file Word.
File sau khi chèn ảnh sẽ được upload lại lên S3 (overwrite file gốc).

## Request (multipart/form-data)

**Parameters:**
- `documentId` (required): GUID của document cần chèn chữ ký
- `file` (required): File ảnh chữ ký (PNG, JPG, JPEG)

**Example:**
```bash
curl -X POST http://localhost:5000/api/contracts/sign-document \
  -F ""documentId=123e4567-e89b-12d3-a456-426614174000"" \
  -F ""file=@signature.png""
```

## Response
```json
{
  ""success"": true,
  ""data"": {
    ""documentId"": ""123e4567-e89b-12d3-a456-426614174000"",
    ""fileUrl"": ""https://s3.../contracts/filled/HOP_DONG_XXX.docx"",
    ""fileName"": ""HOP_DONG_XXX.docx""
  },
  ""message"": ""Signature inserted successfully""
}
```

## Flow
1. Validate documentId và signature image file
2. Download document từ S3
3. Chèn ảnh vào Content Control với tag 'DigitalSignature'
4. Upload file đã chỉnh sửa lên S3 (overwrite file gốc)
5. Return document info

## Content Control Requirements
Document Word phải có Content Control với:
- **Tag**: `DigitalSignature`
- **Type**: Rich Text hoặc Picture

## Supported Image Formats
- PNG (recommended)
- JPG/JPEG

## Notes
- File gốc sẽ bị overwrite với version có chữ ký
- Ảnh sẽ được resize về kích thước 200x80 pixels
- Content Control phải tồn tại trong document, nếu không sẽ báo lỗi
");
    }
}
