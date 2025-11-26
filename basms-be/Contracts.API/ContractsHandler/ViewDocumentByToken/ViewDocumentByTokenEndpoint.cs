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

                        // Validate result có file stream không
                        if (result.FileStream == null || string.IsNullOrEmpty(result.ContentType))
                        {
                            logger.LogError("File stream is null after successful query");
                            return Results.Problem(
                                title: "Document retrieval failed",
                                detail: "File stream is not available",
                                statusCode: StatusCodes.Status500InternalServerError
                            );
                        }

                        logger.LogInformation(
                            "Successfully retrieved document: {DocumentId} - {FileName} ({ContentType})",
                            result.DocumentId, result.FileName, result.ContentType);

                        // Trả về file stream với correct Content-Type
                        // Browser sẽ hiển thị PDF inline, hoặc download file Word
                        return Results.File(
                            fileStream: result.FileStream,
                            contentType: result.ContentType,
                            fileDownloadName: result.FileName,
                            enableRangeProcessing: true  // Hỗ trợ partial content (video/pdf streaming)
                        );
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
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Xem document bằng documentId và security token (dùng cho ký điện tử)")
            .WithDescription(@"
## Mô tả
Endpoint này cho phép xem document đã được fill bằng documentId và security token.
Token được tạo tự động sau khi fill template thành công.

## Use Case
- Frontend hiển thị document trong PDF viewer trước khi ký
- Người ký xem trước nội dung hợp đồng
- Không yêu cầu authentication, chỉ cần documentId và token hợp lệ

## Security
- Yêu cầu cả documentId (route param) và token (query param) phải khớp
- Token có thời hạn (mặc định 7 ngày)
- Token chỉ dùng 1 lần (invalidated sau khi ký)
- Mọi truy cập được log để audit

## Response Types
- **PDF**: Browser hiển thị inline trong PDF viewer
- **Word**: Browser tự động download file

## Ví dụ
```
GET /api/contracts/documents/123e4567-e89b-12d3-a456-426614174000/view?token=a1b2c3d4-e5f6-4789-b012-3456789abcde

Response Headers:
  Content-Type: application/pdf
  Content-Disposition: inline; filename=""contract.pdf""

Response Body: [PDF binary stream]
```

## Error Codes
- `TOKEN_REQUIRED`: Token không được cung cấp
- `INVALID_TOKEN`: Token hoặc documentId không khớp hoặc đã bị vô hiệu hóa
- `TOKEN_EXPIRED`: Token đã hết hạn
- `DOWNLOAD_FAILED`: Không thể tải file từ S3
            ");
    }
}
