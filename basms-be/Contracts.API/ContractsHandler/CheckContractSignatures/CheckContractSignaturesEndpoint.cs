namespace Contracts.API.ContractsHandler.CheckContractSignatures;

public class CheckContractSignaturesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/documents/{documentId}/signatures/check
        app.MapGet("/api/contracts/documents/{documentId}/signatures/check",
            async (Guid documentId, ISender sender, ILogger<CheckContractSignaturesEndpoint> logger) =>
            {
                try
                {
                    logger.LogInformation("Checking signatures for DocumentId: {DocumentId}", documentId);

                    var command = new CheckContractSignaturesCommand(documentId);
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
                            signatureCount = result.SignatureCount,
                            signatures = result.Signatures,
                            hasEmployeeSignature = result.HasEmployeeSignature,
                            message = result.HasEmployeeSignature
                                ? "Hợp đồng đã có chữ ký của Bên B (Người lao động)"
                                : "Hợp đồng chưa có chữ ký của Bên B"
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in check contract signatures endpoint");
                    return Results.Problem(
                        title: "Signature check failed",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            })
        .WithTags("Contracts")
        .WithName("CheckContractSignatures")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Kiểm tra chữ ký điện tử trong hợp đồng")
        .WithDescription(@"
Kiểm tra số lượng và tính hợp lệ của chữ ký điện tử trong hợp đồng Word.

**Business Logic:**
- Tải file hợp đồng từ S3
- Kiểm tra các chữ ký điện tử có trong document
- Xác nhận có ít nhất 1 chữ ký của Bên B (Người lao động)

**Response:**
- signatureCount: Số lượng chữ ký tìm thấy
- signatures: Danh sách thông tin chữ ký
- hasEmployeeSignature: true nếu có ít nhất 1 chữ ký

**Example:**
```bash
curl -X GET http://localhost:5000/api/contracts/documents/550e8400-e29b-41d4-a716-446655440000/signatures/check
```
");
    }
}
