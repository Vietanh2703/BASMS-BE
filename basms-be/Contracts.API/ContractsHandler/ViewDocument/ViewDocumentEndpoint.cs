namespace Contracts.API.ContractsHandler.ViewDocument;

public class ViewDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/documents/{documentId}
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
                            // Xác định status code dựa trên error code
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
            .AllowAnonymous()  // Có thể thay đổi thành RequireAuthorization() nếu cần authentication
            .WithTags("Contracts")
            .WithName("ViewDocument")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Lấy pre-signed URL để xem document (không cần token)")
            .WithDescription(@"
## Mô tả
Endpoint này trả về **S3 pre-signed URL** để truy cập trực tiếp file từ S3.
Không yêu cầu token, chỉ cần documentId hợp lệ.

## Use Case
- Xem document đã được ký (signed documents)
- Internal access cho admin/staff
- Xem document trong hệ thống quản lý
- Download document cho mục đích audit

## Security
- **Lưu ý**: Endpoint này không validate token, nên cân nhắc thêm authentication
- Pre-signed URL có thời hạn 15 phút
- Mọi truy cập được log để audit
- Chỉ truy cập được document chưa bị xóa (IsDeleted = 0)

## Performance Benefits
- **Faster**: Download trực tiếp từ S3, không qua backend
- **Scalable**: S3 handle file serving, backend chỉ generate URL
- **Bandwidth**: Không tốn bandwidth backend

## Ví dụ

**Request:**
```
GET /api/contracts/documents/123e4567-e89b-12d3-a456-426614174000
```

**Response:**
```json
{
  ""success"": true,
  ""data"": {
    ""fileUrl"": ""https://bucket.s3.region.amazonaws.com/contracts/signed/file.docx?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=..."",
    ""fileName"": ""SIGNED_contract_2025.docx"",
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
const response = await fetch(`/api/contracts/documents/${documentId}`);
const { data } = await response.json();

// Use pre-signed URL directly
window.open(data.fileUrl); // Download
// OR
<iframe src={data.fileUrl} /> // Display
// OR
<embed src={data.fileUrl} type=""application/pdf"" /> // PDF viewer
```

## So sánh với /view endpoint
- **ViewDocumentByToken** (`/view?token=...`): Dùng token, cho external users, để ký điện tử
- **ViewDocument** (endpoint này): Không cần token, cho internal access, đã authenticated

## Error Codes
- `DOCUMENT_NOT_FOUND`: Document không tồn tại hoặc đã bị xóa
- `URL_GENERATION_FAILED`: Không thể tạo pre-signed URL từ S3
- `INTERNAL_ERROR`: Lỗi server nội bộ
            ");
    }
}
