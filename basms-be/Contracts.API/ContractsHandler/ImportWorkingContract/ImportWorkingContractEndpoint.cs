namespace Contracts.API.ContractsHandler.ImportWorkingContract;

public class ImportWorkingContractEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /api/contracts/working/import
        app.MapPost("/api/contracts/working/import",
                async (HttpRequest request, ISender sender, ILogger<ImportWorkingContractEndpoint> logger) =>
                {
                    try
                    {
                        Guid documentId;

                        // Hỗ trợ cả JSON và Form-Data
                        if (request.HasFormContentType)
                        {
                            var documentIdStr = request.Form["documentId"].ToString();
                            if (!Guid.TryParse(documentIdStr, out documentId))
                                return Results.BadRequest(new { success = false, error = "Invalid documentId format" });
                        }
                        else if (request.ContentType?.Contains("application/json") == true)
                        {
                            var req = await request.ReadFromJsonAsync<ImportWorkingContractRequest>();
                            if (req == null)
                                return Results.BadRequest(new { success = false, error = "Invalid request body" });
                            documentId = req.DocumentId;
                        }
                        else
                        {
                            return Results.BadRequest(new
                            {
                                success = false,
                                error = "Unsupported content type. Use application/json or multipart/form-data"
                            });
                        }

                        logger.LogInformation("Importing working contract from DocumentId: {DocumentId}", documentId);

                        var command = new ImportWorkingContractCommand(documentId);
                        var result = await sender.Send(command);

                        if (!result.Success)
                            return Results.BadRequest(new { success = false, error = result.ErrorMessage });

                        return Results.Ok(new
                        {
                            success = true,
                            data = new
                            {
                                contractId = result.ContractId,
                                userId = result.UserId,
                                contractNumber = result.ContractNumber,
                                contractTitle = result.ContractTitle,
                                employeeName = result.EmployeeName,
                                employeeEmail = result.EmployeeEmail,
                                contractType = "working_contract",
                                status = "signed",
                                message = "Hợp đồng lao động đã được import thành công."
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error in import working contract endpoint");
                        return Results.Problem(
                            title: "Import failed",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status500InternalServerError
                        );
                    }
                })
            .WithTags("Contracts")
            .WithName("ImportWorkingContract")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Import hợp đồng lao động từ Word document đã ký")
            .WithDescription(@"
Import working contract (hợp đồng lao động bảo vệ) từ Word document đã được ký.
Tự động tạo user mới cho người lao động và customer tương ứng.

**Business Logic:**
1. Kiểm tra document tồn tại
2. Kiểm tra có ít nhất 1 chữ ký điện tử (Bên B - Người lao động)
3. Extract thông tin từ Word document:
   - **Thông tin hợp đồng:** ContractNumber, StartDate, EndDate
   - **Thông tin người lao động (Bên B):**
     * Họ và tên
     * Sinh ngày, nơi sinh
     * Số CCCD, ngày cấp, nơi cấp
     * Hộ khẩu thường trú
     * Điện thoại
     * Email
4. Tạo User mới (Guard role) với:
   - Password: random 9 ký tự (sẽ được gửi qua email)
   - Status: active
   - EmailVerified: false
   - RoleId: guard (ddbd6230-ba6e-11f0-bcac-00155dca8f48)
5. Đợi UserCreatedConsumer tự động tạo Customer
6. Tạo Contract với:
   - CustomerId: từ customer vừa tạo
   - ContractType: ""working_contract""
   - Status: ""signed""
   - DocumentId: link đến document
7. Lưu vào database

**Request Body:**
```json
{
  ""documentId"": ""guid-of-signed-document""
}
```

**Example using curl:**
```bash
curl -X POST http://localhost:5000/api/contracts/working/import \
  -H ""Content-Type: application/json"" \
  -d '{
    ""documentId"": ""550e8400-e29b-41d4-a716-446655440000""
  }'
```

**Response:**
```json
{
  ""success"": true,
  ""data"": {
    ""contractId"": ""guid"",
    ""userId"": ""guid"",
    ""customerId"": ""guid"",
    ""contractNumber"": ""001"",
    ""contractTitle"": ""Hợp đồng lao động - Tran Van C - 14/11/2025"",
    ""employeeName"": ""Tran Van C"",
    ""employeeEmail"": ""example@email.com""
  }
}
```

**Validation:**
- Document phải tồn tại và chưa bị xóa
- Document phải có ít nhất 1 chữ ký điện tử (Bên B)
- Email người lao động phải có trong document
- Thời hạn hợp đồng không vượt quá 36 tháng
");
    }
}

/// <summary>
///     Request model cho import working contract
///     Chỉ cần DocumentId - guard
/// </summary>
public record ImportWorkingContractRequest(
    Guid DocumentId
);