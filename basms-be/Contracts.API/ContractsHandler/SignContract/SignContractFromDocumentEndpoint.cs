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

                    // Optional: Customer info for email notification
                    var customerEmail = form["customerEmail"].ToString();
                    var customerName = form["customerName"].ToString();
                    var contractNumber = form["contractNumber"].ToString();

                    logger.LogInformation(
                        "Inserting signature - DocumentId: {DocumentId}, Image: {FileName} ({Size} bytes), Customer: {CustomerName} ({CustomerEmail})",
                        documentId, signatureImage.FileName, signatureImage.Length,
                        string.IsNullOrEmpty(customerName) ? "N/A" : customerName,
                        string.IsNullOrEmpty(customerEmail) ? "N/A" : customerEmail);

                    // ================================================================
                    // TẠO COMMAND VÀ GỬI ĐẾN HANDLER
                    // ================================================================
                    var command = new SignContractFromDocumentCommand(
                        DocumentId: documentId,
                        SignatureImage: signatureImage,
                        CustomerEmail: string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
                        CustomerName: string.IsNullOrWhiteSpace(customerName) ? null : customerName,
                        ContractNumber: string.IsNullOrWhiteSpace(contractNumber) ? null : contractNumber
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
File sau khi chèn ảnh sẽ được đổi tên thành ""Signed_..."" và chuyển vào thư mục ""contracts/signed/"".

## Request (multipart/form-data)

**Parameters:**
- `documentId` (required): GUID của document cần chèn chữ ký
- `file` (required): File ảnh chữ ký (PNG, JPG, JPEG)
- `customerEmail` (optional): Email khách hàng để gửi thông báo xác nhận
- `customerName` (optional): Tên khách hàng
- `contractNumber` (optional): Số hợp đồng

**Example:**
```bash
curl -X POST http://localhost:5000/api/contracts/sign-document \
  -F ""documentId=123e4567-e89b-12d3-a456-426614174000"" \
  -F ""file=@signature.png"" \
  -F ""customerEmail=customer@example.com"" \
  -F ""customerName=Nguyen Van A"" \
  -F ""contractNumber=HD-2025-001""
```

## Response
```json
{
  ""success"": true,
  ""data"": {
    ""documentId"": ""123e4567-e89b-12d3-a456-426614174000"",
    ""fileUrl"": ""https://s3.../contracts/signed/Signed_HOP_DONG_XXX.docx"",
    ""fileName"": ""Signed_HOP_DONG_XXX.docx""
  },
  ""message"": ""Signature inserted successfully""
}
```

## Flow
1. Validate documentId và signature image file
2. Download document từ S3
3. Chèn ảnh vào Content Control với tag 'DigitalSignature'
4. Upload file đã chỉnh sửa vào thư mục ""contracts/signed/"" với tên ""Signed_...""
5. Cập nhật database (tên file, đường dẫn, version=signed, xóa token)
6. Gửi email xác nhận cho khách hàng (nếu có thông tin)
7. Return document info

## Email Notification
Nếu cung cấp đầy đủ thông tin customer (email, tên, số hợp đồng), hệ thống sẽ tự động gửi email xác nhận với nội dung:
- ✅ Xác nhận chữ ký thành công
- 📋 Thông tin hợp đồng đã ký
- 📌 Các bước tiếp theo (xét duyệt, triển khai)
- 📧 Nhắc nhở theo dõi email để nhận thông báo

## Content Control Requirements
Document Word phải có Content Control với:
- **Tag**: `DigitalSignature`
- **Type**: Rich Text hoặc Picture

## Supported Image Formats
- PNG (recommended)
- JPG/JPEG

## Notes
- File sẽ được đổi tên từ ""FILLED_..."" thành ""Signed_...""
- File sẽ được chuyển từ ""contracts/filled/"" sang ""contracts/signed/""
- Ảnh sẽ được resize về kích thước 200x80 pixels
- Token và TokenExpiredDay sẽ bị xóa khỏi database
- Version sẽ được cập nhật thành ""signed""
- Content Control phải tồn tại trong document, nếu không sẽ báo lỗi
");
    }
}
