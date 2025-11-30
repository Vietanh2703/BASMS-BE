namespace Contracts.API.ContractsHandler.DownloadContractDocument;

/// <summary>
/// Endpoint để download contract document từ AWS S3
/// </summary>
public class DownloadContractDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/documents/{documentId}/download
        app.MapGet("/api/contracts/documents/{documentId:guid}/download", async (
            Guid documentId,
            ISender sender,
            ILogger<DownloadContractDocumentEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Download request for document: {DocumentId}", documentId);

                var query = new DownloadContractDocumentQuery(documentId);
                var result = await sender.Send(query);

                if (!result.Success || result.FileStream == null)
                {
                    logger.LogWarning(
                        "Download failed for document {DocumentId}: {ErrorMessage}",
                        documentId,
                        result.ErrorMessage);

                    return Results.NotFound(new
                    {
                        success = false,
                        message = result.ErrorMessage ?? "Document not found or download failed"
                    });
                }

                logger.LogInformation(
                    "Successfully prepared download for: {FileName}",
                    result.FileName);

                // Trả về file stream với proper headers
                return Results.File(
                    fileStream: result.FileStream,
                    contentType: result.ContentType ?? "application/octet-stream",
                    fileDownloadName: result.FileName,
                    enableRangeProcessing: true
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing download request for document: {DocumentId}", documentId);
                return Results.Problem(
                    title: "Error downloading document",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Documents")
        .WithName("DownloadContractDocument")
        .Produces<FileStreamResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Download contract document từ AWS S3")
        .WithDescription(@"
            Endpoint này cho phép download file contract document đã được lưu trữ trên AWS S3.

            FLOW:
            1. Lấy thông tin document từ database bằng DocumentId
            2. Download file từ S3 sử dụng FileUrl
            3. Trả về file stream với proper headers

            INPUT:
            - documentId (GUID): ID của document trong bảng contract_documents

            OUTPUT:
            - File stream để download
            - Headers:
              * Content-Type: MIME type của file (application/pdf, image/jpeg, etc.)
              * Content-Disposition: attachment; filename=""contract.pdf""
              * Accept-Ranges: bytes (support resume download)

            VÍ DỤ SỬ DỤNG:
            ===============

            **Browser / cURL:**
            ```bash
            curl -O -J 'http://localhost:5000/api/contracts/documents/{documentId}/download'
            ```

            **JavaScript Fetch:**
            ```javascript
            fetch('/api/contracts/documents/{documentId}/download')
              .then(response => response.blob())
              .then(blob => {
                const url = window.URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = 'contract.pdf';
                a.click();
              });
            ```

            **Postman:**
            1. GET request to: /api/contracts/documents/{documentId}/download
            2. Click 'Send and Download'
            3. File will be saved to your download folder

            ERROR RESPONSES:
            ================
            - 404 Not Found: Document không tồn tại hoặc đã bị xóa
            - 500 Internal Server Error: Lỗi khi download từ S3 hoặc lỗi hệ thống

            LƯU Ý:
            =======
            - File được stream trực tiếp từ S3, không lưu vào server
            - Support resume download (Range requests)
            - File stream sẽ tự động dispose sau khi download xong
        ");
    }
}
