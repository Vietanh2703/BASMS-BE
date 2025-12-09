namespace Contracts.API.ContractsHandler.GetDocumentById;

public class GetDocumentByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/documents/details/{documentId:guid}", async (
            Guid documentId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetDocumentByIdQuery(documentId);
            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                return Results.NotFound(new
                {
                    success = false,
                    message = result.ErrorMessage
                });
            }

            return Results.Ok(new
            {
                success = true,
                data = result.Document
            });
        })
        .RequireAuthorization()
        .WithName("GetDocumentById")
        .WithTags("Contracts - Documents")
        .WithSummary("Lấy thông tin chi tiết document theo ID")
        .WithDescription(@"
Lấy thông tin đầy đủ của contract document bao gồm:
- Document metadata (loại tài liệu, tên file, version...)
- File information (URL, size, MIME type...)
- Document dates (ngày tài liệu, ngày bắt đầu/kết thúc, ngày ký...)
- Approval information (người approve, ngày approve...)
- Upload information (người upload, email, tên khách hàng...)

## Response Structure:
```json
{
  ""success"": true,
  ""data"": {
    ""id"": ""guid"",
    ""documentType"": ""contract"",
    ""category"": ""service_contract"",
    ""documentName"": ""Hợp đồng dịch vụ bảo vệ.pdf"",
    ""fileUrl"": ""https://s3.amazonaws.com/..."",
    ""fileSize"": 2048576,
    ""version"": ""1.0"",
    ""documentDate"": ""2025-01-01T00:00:00Z"",
    ""startDate"": ""2025-01-01T00:00:00Z"",
    ""endDate"": ""2025-12-31T23:59:59Z"",
    ""signDate"": ""2025-01-15T10:30:00Z"",
    ""approvedAt"": ""2025-01-14T15:00:00Z"",
    ""approvedBy"": ""guid"",
    ""uploadedBy"": ""guid"",
    ""documentEmail"": ""customer@example.com"",
    ""documentCustomerName"": ""Công ty ABC"",
    ""tokens"": null,
    ""tokenExpiredDay"": null,
    ""createdAt"": ""2025-01-01T00:00:00Z""
  }
}
```

## Error Response (404):
```json
{
  ""success"": false,
  ""message"": ""Document with ID {documentId} not found""
}
```
")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces<object>(StatusCodes.Status404NotFound);
    }
}
