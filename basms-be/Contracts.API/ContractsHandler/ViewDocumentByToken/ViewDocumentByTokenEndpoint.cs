namespace Contracts.API.ContractsHandler.ViewDocumentByToken;

public class ViewDocumentByTokenEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/documents/{documentId}/view?token={securityToken}
        app.MapGet("/api/contracts/documents/{documentId:guid}/view",
                async (
                    Guid documentId,
                    string? token,
                    ISender sender,
                    ILogger<ViewDocumentByTokenEndpoint> logger) =>
                {
                    try
                    {
                        // Validate token parameter
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
                            // Xác định status code dựa trên error code
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

                        // Validate result có file URL không
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

                        // Trả về JSON với pre-signed URL
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
            .AllowAnonymous()  // Không cần authentication, chỉ cần token
            .WithTags("Contracts")
            .WithName("ViewDocumentByToken")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Lấy pre-signed URL để xem document (dùng cho ký điện tử)")
            .WithDescription(@"
## Mô tả
Endpoint này trả về **S3 pre-signed URL** để frontend có thể truy cập trực tiếp file từ S3.
Token được tạo tự động sau khi fill template thành công.

## Use Case
- Frontend nhận pre-signed URL và hiển thị document trong PDF viewer
- Người ký xem trước nội dung hợp đồng
- Không yêu cầu authentication, chỉ cần documentId và token hợp lệ
- File được download trực tiếp từ S3 (không qua backend)

## Security
- Yêu cầu cả documentId (route param) và token (query param) phải khớp
- Document token có thời hạn (mặc định 7 ngày)
- Pre-signed URL có thời hạn 15 phút
- Token chỉ dùng 1 lần (invalidated sau khi ký)
- Mọi truy cập được log để audit

## Performance Benefits
- **Faster**: Download trực tiếp từ S3, không qua backend
- **Scalable**: S3 handle file serving, backend chỉ generate URL
- **Bandwidth**: Không tốn bandwidth backend

## Ví dụ

**Request:**
```
GET /api/contracts/documents/123e4567-e89b-12d3-a456-426614174000/view?token=a1b2c3d4-e5f6-4789-b012-3456789abcde
```

**Response:**
```json
{
  ""success"": true,
  ""data"": {
    ""fileUrl"": ""https://bucket.s3.region.amazonaws.com/contracts/filled/file.docx?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=..."",
    ""fileName"": ""FILLED_contract_2025.docx"",
    ""contentType"": ""application/vnd.openxmlformats-officedocument.wordprocessingml.document"",
    ""fileSize"": 123456,
    ""documentId"": ""123e4567-e89b-12d3-a456-426614174000"",
    ""urlExpiresAt"": ""2025-11-26T07:00:00Z""
  }
}
```

**Frontend Usage:**
```javascript
// Get URL from API
const response = await fetch(apiUrl);
const { data } = await response.json();

// Use pre-signed URL directly
window.open(data.fileUrl); // Download
// OR
<iframe src={data.fileUrl} /> // Display
// OR
<embed src={data.fileUrl} type=""application/pdf"" /> // PDF viewer
```

## Error Codes
- `TOKEN_REQUIRED`: Token không được cung cấp
- `INVALID_TOKEN`: Token hoặc documentId không khớp hoặc đã bị vô hiệu hóa
- `TOKEN_EXPIRED`: Token đã hết hạn
- `URL_GENERATION_FAILED`: Không thể tạo pre-signed URL từ S3
            ");
    }
}
