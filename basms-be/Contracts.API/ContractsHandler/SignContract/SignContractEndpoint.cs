namespace Contracts.API.ContractsHandler.SignContract;

public class SignContractEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /api/contracts/sign
        app.MapPost("/api/contracts/sign",
    async (HttpRequest request, ISender sender, ILogger<SignContractEndpoint> logger) =>
    {
        try
        {
            if (!request.HasFormContentType || request.Form.Files.Count == 0)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    error = "No document file uploaded"
                });
            }

            var documentFile = request.Form.Files["file"];
            var certificateFile = request.Form.Files["certificate"];
            
            if (documentFile == null)
            {
                return Results.BadRequest(new { success = false, error = "Document file is required" });
            }

            if (certificateFile == null)
            {
                return Results.BadRequest(new { success = false, error = "Certificate file is required" });
            }

            var certificatePassword = request.Form["certificatePassword"].ToString();

            if (string.IsNullOrEmpty(certificatePassword))
            {
                return Results.BadRequest(new { success = false, error = "Certificate password is required" });
            }

            // Validate file extensions
            if (!documentFile.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { success = false, error = "Only .docx files are supported" });
            }

            if (!certificateFile.FileName.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { success = false, error = "Only .pfx certificate files are supported" });
            }

            // Lưu certificate tạm thời
            var tempCertPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pfx");
            
            using (var certStream = certificateFile.OpenReadStream())
            using (var fileStream = File.Create(tempCertPath))
            {
                await certStream.CopyToAsync(fileStream);
            }

            logger.LogInformation("Signing document {FileName} with uploaded certificate",
                documentFile.FileName);

            var outputFileName = $"Signed_{documentFile.FileName}";

            using var documentStream = documentFile.OpenReadStream();
            var command = new SignContractCommand(
                DocumentStream: documentStream,
                CertificatePath: tempCertPath,
                CertificatePassword: certificatePassword,
                OutputFileName: outputFileName
            );

            var result = await sender.Send(command);

            // Xóa file certificate tạm
            try { File.Delete(tempCertPath); } catch { }

            if (!result.Success || result.SignedDocumentStream == null)
            {
                return Results.BadRequest(new { success = false, error = result.ErrorMessage });
            }

            return Results.File(
                fileStream: result.SignedDocumentStream,
                contentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileDownloadName: outputFileName
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in sign contract endpoint");
            return Results.Problem(
                title: "Sign contract failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    })

        .DisableAntiforgery()
        .WithTags("Contracts")
        .WithName("SignContract")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Ký điện tử hợp đồng Word")
        .WithDescription(@"
Upload Word document và ký điện tử bằng certificate.

**Form fields:**
- `file` (required): Word document file (.docx) cần ký
- `certificatePath` (required): Đường dẫn đến file certificate (.pfx)
- `certificatePassword` (required): Mật khẩu của certificate

**Example using curl:**
```bash
curl -X POST http://localhost:5000/api/contracts/sign \
  -F 'file=@contract.docx' \
  -F 'certificatePath=C:/certs/company.pfx' \
  -F 'certificatePassword=yourpassword' \
  --output signed_contract.docx
```

**Response:**
Trả về file Word đã được ký điện tử.

**Flow:**
1. Bên A (Công ty) ký trước với certificate của công ty
2. Download file đã ký, gửi cho Bên B (Người lao động)
3. Bên B ký tiếp với certificate cá nhân
4. Document cuối cùng có 2 chữ ký điện tử
5. Upload lên S3 và import vào hệ thống
");
    }
}
