namespace Contracts.API.ContractsHandler.ImportContractFromDocument;

/// <summary>
/// Endpoint để upload và import contract từ file Word/PDF
/// Upload and import contract from Word/PDF document
/// </summary>
public class ImportContractFromDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /api/contracts/import-from-document
        app.MapPost("/api/contracts/import-from-document", async (
            HttpRequest request,
            ISender sender,
            ILogger<ImportContractFromDocumentEndpoint> logger) =>
        {
            try
            {
                // Kiểm tra có file không
                if (!request.HasFormContentType || request.Form.Files.Count == 0)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "No file uploaded. Please upload a Word (.docx) or PDF (.pdf) contract file."
                    });
                }

                var file = request.Form.Files[0];

                // Validate file extension
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (extension != ".docx" && extension != ".pdf")
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = $"Invalid file type: {extension}. Only .docx and .pdf files are supported."
                    });
                }

                // Validate file size (max 10MB)
                const long maxFileSize = 10 * 1024 * 1024; // 10MB
                if (file.Length > maxFileSize)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = $"File too large. Maximum size is 10MB. Your file: {file.Length / 1024 / 1024}MB"
                    });
                }

                // Lấy CreatedBy từ form hoặc claims (tạm thời dùng Guid.Empty nếu không có)
                var createdByString = request.Form["createdBy"].FirstOrDefault();
                var createdBy = Guid.TryParse(createdByString, out var userId) ? userId : Guid.Empty;

                logger.LogInformation(
                    "Importing contract from document: {FileName} ({FileSize} bytes)",
                    file.FileName,
                    file.Length);

                // Copy stream để tránh disposed
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Tạo command và gửi
                var command = new ImportContractFromDocumentCommand(
                    FileStream: memoryStream,
                    FileName: file.FileName,
                    CreatedBy: createdBy
                );

                var result = await sender.Send(command);

                if (result.Success)
                {
                    logger.LogInformation(
                        "Successfully imported contract {ContractId} from document {FileName}",
                        result.ContractId,
                        file.FileName);

                    return Results.Ok(result);
                }
                else
                {
                    logger.LogWarning(
                        "Failed to import contract from document {FileName}: {ErrorMessage}",
                        file.FileName,
                        result.ErrorMessage);

                    return Results.BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error importing contract from document");
                return Results.Problem(
                    title: "Error importing contract",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithTags("Contracts")
        .WithName("ImportContractFromDocument")
        .Produces<ImportContractFromDocumentResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .DisableAntiforgery() // Cần thiết cho file upload
        .WithSummary("Import contract từ file Word/PDF")
        .WithDescription(@"
            Endpoint này cho phép upload file hợp đồng (.docx hoặc .pdf) và tự động:

            1. TRÍCH XUẤT THÔNG TIN:
               - Đọc nội dung từ file Word/PDF
               - Phân tích và trích xuất các thông tin quan trọng:
                 * Số hợp đồng (Contract Number)
                 * Ngày bắt đầu, kết thúc (Start/End Date)
                 * Thông tin khách hàng (Customer Name, Address, Phone, Email)
                 * Số lượng bảo vệ yêu cầu (Guards Required)
                 * Lịch ca làm việc (Shift Schedules)

            2. LƯU VÀO DATABASE:
               - Tạo/tìm Customer record
               - Tạo Contract mới (status = 'draft' để review)
               - Tạo Location mặc định
               - Tạo ContractLocation link
               - Tạo các ContractShiftSchedule

            3. TRẢ VỀ KẾT QUẢ:
               - Success flag
               - Contract ID, Customer ID, Location IDs, Schedule IDs
               - Confidence score (độ tin cậy của việc trích xuất: 0-100%)
               - Warnings (nếu có thông tin nào không trích xuất được)

            FILE YÊU CẦU:
            =============
            - Định dạng: .docx hoặc .pdf
            - Kích thước tối đa: 10MB
            - Nội dung nên có các thông tin:
              * Số hợp đồng: 'Số HĐ: XXX' hoặc 'Contract No: XXX'
              * Thời hạn: 'Từ ngày ... đến ngày ...'
              * Khách hàng: 'Bên B: Công ty...' hoặc 'Khách hàng: ...'
              * Số bảo vệ: 'X bảo vệ' hoặc 'Số lượng: X'
              * Ca làm: 'Ca sáng: 8h-17h', 'Ca tối: 18h-22h'

            CÁCH SỬ DỤNG:
            =============

            **cURL Example:**
            ```bash
            curl -X POST 'http://localhost:5000/api/contracts/import-from-document' \
              -F 'file=@contract.docx' \
              -F 'createdBy=00000000-0000-0000-0000-000000000001'
            ```

            **HTML Form Example:**
            ```html
            <form method='post' enctype='multipart/form-data'
                  action='/api/contracts/import-from-document'>
              <input type='file' name='file' accept='.docx,.pdf' />
              <input type='hidden' name='createdBy' value='user-guid' />
              <button type='submit'>Import Contract</button>
            </form>
            ```

            SAU KHI IMPORT:
            ===============
            1. Review contract trong database (status = 'draft')
            2. Chỉnh sửa/bổ sung thông tin nếu cần
            3. Activate contract → ContractActivatedEvent
            4. Shifts.API tự động generate shifts từ contract schedules

            VÍ DỤ RESPONSE:
            ===============
            ```json
            {
              'success': true,
              'message': 'Contract imported successfully',
              'contractId': 'guid-xxx',
              'customerId': 'guid-yyy',
              'locationIds': ['guid-zzz'],
              'scheduleIds': ['guid-aaa', 'guid-bbb'],
              'confidenceScore': 85,
              'warnings': [
                'Could not extract customer phone number',
                'Shift end time not found for schedule 2'
              ]
            }
            ```

            LƯU Ý:
            ======
            - Contract được tạo ở trạng thái 'draft' để admin review trước khi activate
            - Confidence score thấp (<50%) nghĩa là nên kiểm tra kỹ thông tin
            - Warnings cho biết thông tin nào không tìm thấy, cần nhập thủ công
        ");
    }
}
