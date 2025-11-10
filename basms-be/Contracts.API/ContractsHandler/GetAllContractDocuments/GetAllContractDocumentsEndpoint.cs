namespace Contracts.API.ContractsHandler.GetAllContractDocuments;

/// <summary>
/// Endpoint để lấy tất cả contract documents từ AWS S3
/// </summary>
public class GetAllContractDocumentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/documents
        app.MapGet("/contracts/documents", async (
            ISender sender,
            ILogger<GetAllContractDocumentsEndpoint> logger) =>
        {
            try
            {
                logger.LogInformation("Get all contract documents request");

                var query = new GetAllContractDocumentsQuery();
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogError("Failed to get documents: {ErrorMessage}", result.ErrorMessage);
                    return Results.Problem(
                        title: "Error getting documents",
                        detail: result.ErrorMessage,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }

                logger.LogInformation(
                    "Successfully retrieved {Count} contract documents",
                    result.TotalCount);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing get all documents request");
                return Results.Problem(
                    title: "Error getting documents",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts - Documents")
        .WithName("GetAllContractDocuments")
        .Produces<GetAllContractDocumentsResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Lấy tất cả contract documents từ AWS S3")
        .WithDescription(@"
            Endpoint này trả về danh sách tất cả contract documents đã được upload lên AWS S3.

            FLOW:
            1. Query tất cả documents từ bảng contract_documents (IsDeleted = 0)
            2. Map sang DTO với thông tin bổ sung (FileSizeFormatted, DownloadUrl)
            3. Trả về danh sách đầy đủ

            OUTPUT:
            - success: true/false
            - documents: Array of ContractDocumentDto
              * id: GUID của document
              * documentType: Loại tài liệu (contract, amendment, appendix, etc.)
              * documentName: Tên file gốc
              * fileUrl: URL file trên S3
              * fileSize: Kích thước (bytes)
              * fileSizeFormatted: Kích thước dạng human-readable (e.g., ""2.5 MB"")
              * mimeType: MIME type của file
              * version: Phiên bản tài liệu
              * documentDate: Ngày tài liệu
              * uploadedBy: User ID người upload
              * createdAt: Ngày tạo
              * downloadUrl: URL để download file (/api/contracts/documents/{id}/download)
            - totalCount: Tổng số documents

            VÍ DỤ RESPONSE:
            ===============
            ```json
            {
              ""success"": true,
              ""documents"": [
                {
                  ""id"": ""guid-xxx-xxx"",
                  ""documentType"": ""contract"",
                  ""documentName"": ""contract-2025-001.pdf"",
                  ""fileUrl"": ""https://basms-contracts.s3.ap-southeast-1.amazonaws.com/contracts/2025/01/10/guid_contract.pdf"",
                  ""fileSize"": 2548736,
                  ""fileSizeFormatted"": ""2.43 MB"",
                  ""mimeType"": ""application/pdf"",
                  ""version"": ""1.0"",
                  ""documentDate"": ""2025-01-10T00:00:00Z"",
                  ""uploadedBy"": ""guid-user-xxx"",
                  ""createdAt"": ""2025-01-10T08:30:00Z"",
                  ""downloadUrl"": ""/api/contracts/documents/guid-xxx-xxx/download""
                },
                {
                  ""id"": ""guid-yyy-yyy"",
                  ""documentType"": ""amendment"",
                  ""documentName"": ""amendment-2025-001.docx"",
                  ""fileUrl"": ""https://basms-contracts.s3.ap-southeast-1.amazonaws.com/contracts/2025/01/09/guid_amendment.docx"",
                  ""fileSize"": 1048576,
                  ""fileSizeFormatted"": ""1 MB"",
                  ""mimeType"": ""application/vnd.openxmlformats-officedocument.wordprocessingml.document"",
                  ""version"": ""1.0"",
                  ""documentDate"": ""2025-01-09T00:00:00Z"",
                  ""uploadedBy"": ""guid-user-yyy"",
                  ""createdAt"": ""2025-01-09T10:15:00Z"",
                  ""downloadUrl"": ""/api/contracts/documents/guid-yyy-yyy/download""
                }
              ],
              ""totalCount"": 2
            }
            ```

            CÁCH SỬ DỤNG:
            =============

            **cURL:**
            ```bash
            curl -X GET 'http://localhost:5000/api/contracts/documents'
            ```

            **JavaScript Fetch:**
            ```javascript
            fetch('/api/contracts/documents')
              .then(response => response.json())
              .then(data => {
                console.log(`Found ${data.totalCount} documents`);
                data.documents.forEach(doc => {
                  console.log(`${doc.documentName} (${doc.fileSizeFormatted})`);
                  console.log(`Download: ${doc.downloadUrl}`);
                });
              });
            ```

            **Postman:**
            1. GET request to: /api/contracts/documents
            2. Click Send
            3. View JSON response with all documents

            **React Example:**
            ```jsx
            const DocumentsList = () => {
              const [documents, setDocuments] = useState([]);

              useEffect(() => {
                fetch('/api/contracts/documents')
                  .then(res => res.json())
                  .then(data => setDocuments(data.documents));
              }, []);

              return (
                <ul>
                  {documents.map(doc => (
                    <li key={doc.id}>
                      <a href={doc.downloadUrl} download>
                        {doc.documentName} ({doc.fileSizeFormatted})
                      </a>
                    </li>
                  ))}
                </ul>
              );
            };
            ```

            LƯU Ý:
            =======
            - Chỉ trả về documents chưa bị xóa (IsDeleted = 0)
            - Documents được sắp xếp theo ngày tạo (mới nhất trước)
            - FileUrl là URL trực tiếp trên S3 (private, cần presigned URL để access trực tiếp)
            - Sử dụng downloadUrl để download file qua API endpoint
            - FileSizeFormatted tự động format: B, KB, MB, GB, TB
        ");
    }
}