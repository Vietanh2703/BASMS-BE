namespace Contracts.API.ContractsHandler.SignContract;

public class SignContractFromDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/sign-document",
            async (HttpRequest request,
                ISender sender,
                ILogger<SignContractFromDocumentEndpoint> logger) =>
            {
                try
                {
                    // ================================================================
                    // PARSE MULTIPART/FORM-DATA REQUEST
                    // ================================================================
                    if (!request.HasFormContentType)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "Request must be multipart/form-data"
                        });
                    }

                    var form = await request.ReadFormAsync();

                    // Required: documentId hoặc token
                    var documentIdStr = form["documentId"].ToString();
                    var token = form["token"].ToString();

                    if (string.IsNullOrWhiteSpace(documentIdStr) && string.IsNullOrWhiteSpace(token))
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "Either documentId or token is required"
                        });
                    }

                    Guid? documentId = null;
                    if (!string.IsNullOrWhiteSpace(documentIdStr))
                    {
                        if (!Guid.TryParse(documentIdStr, out var parsedId))
                        {
                            return Results.BadRequest(new
                            {
                                success = false,
                                error = "Invalid documentId format"
                            });
                        }
                        documentId = parsedId;
                    }

                    // Optional: Certificate PFX file
                    IFormFile? certificateFile = null;
                    if (form.Files.Count > 0)
                    {
                        certificateFile = form.Files[0];

                        // Validate file extension
                        if (!certificateFile.FileName.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase))
                        {
                            return Results.BadRequest(new
                            {
                                success = false,
                                error = "Certificate file must be .pfx format"
                            });
                        }

                        logger.LogInformation("Certificate file uploaded: {FileName} ({Size} bytes)",
                            certificateFile.FileName, certificateFile.Length);
                    }

                    // Optional: Certificate password
                    var certificatePassword = form["certificatePassword"].ToString();

                    // Validation: Nếu có certificate file thì phải có password
                    if (certificateFile != null && string.IsNullOrWhiteSpace(certificatePassword))
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "Certificate password is required when uploading certificate file"
                        });
                    }

                    logger.LogInformation(
                        "Signing contract - DocumentId: {DocumentId}, Token: {Token}, HasCertificate: {HasCert}",
                        documentId, token != null ? "***" : "null", certificateFile != null);

                    // ================================================================
                    // TẠO COMMAND VÀ GỬI ĐẾN HANDLER
                    // ================================================================
                    var command = new SignContractFromDocumentCommand(
                        DocumentId: documentId,
                        Token: token,
                        CertificateFile: certificateFile,
                        CertificatePassword: certificatePassword
                    );

                    var result = await sender.Send(command);

                    if (!result.Success)
                    {
                        var statusCode = result.ErrorMessage?.Contains("token", StringComparison.OrdinalIgnoreCase) == true
                            ? StatusCodes.Status401Unauthorized
                            : StatusCodes.Status400BadRequest;

                        return Results.Json(new
                        {
                            success = false,
                            error = result.ErrorMessage
                        }, statusCode: statusCode);
                    }

                    return Results.Ok(new
                    {
                        success = true,
                        data = new
                        {
                            signedDocumentId = result.SignedDocumentId,
                            signedFileUrl = result.SignedFileUrl,
                            signedFileName = result.SignedFileName
                        },
                        message = "Contract signed successfully"
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error signing contract");
                    return Results.Problem(
                        title: "Sign contract failed",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            })
            .DisableAntiforgery()
            .AllowAnonymous()  // Cho phép ký bằng token mà không cần auth
            .WithTags("Contracts")
            .WithName("SignContractFromDocument")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Ký điện tử hợp đồng với certificate PFX")
            .WithDescription(@"
## Mô tả
Ký điện tử hợp đồng đã được fill và lưu trên S3.
Hỗ trợ 2 cách xác thực: DocumentId (admin) hoặc Token (người ký qua link)

## Request (multipart/form-data)

### Cách 1: Admin ký với DocumentId
```bash
curl -X POST http://localhost:5000/api/contracts/sign-document \
  -F ""documentId=guid-of-filled-document"" \
  -F ""certificateFile=@certificate.pfx"" \
  -F ""certificatePassword=your-password""
```

### Cách 2: Người dùng ký bằng Token (qua link email)
```bash
curl -X POST http://localhost:5000/api/contracts/sign-document \
  -F ""token=security-token-from-email"" \
  -F ""certificateFile=@certificate.pfx"" \
  -F ""certificatePassword=your-password""
```

### Cách 3: Dùng certificate mặc định từ server config
```bash
curl -X POST http://localhost:5000/api/contracts/sign-document \
  -F ""documentId=guid-of-filled-document""
# Không cần upload certificate, dùng config từ appsettings.json
```

## Response
```json
{
  ""success"": true,
  ""data"": {
    ""signedDocumentId"": ""guid"",
    ""signedFileUrl"": ""https://s3.../contracts/signed/SIGNED_XXX.docx"",
    ""signedFileName"": ""SIGNED_HOP_DONG_XXX.docx""
  },
  ""message"": ""Contract signed successfully""
}
```

## Flow
1. Validate token (nếu dùng token) hoặc documentId
2. Download filled document từ S3
3. Ký với certificate (uploaded hoặc từ config)
4. Upload signed file lên S3
5. Create ContractDocument record với Version=""signed""
6. Invalidate token (nếu dùng token)
7. Return signed document info

## Certificate Priority
1. **Uploaded PFX** (highest priority) - User upload certificate.pfx + password
2. **Server Config** (fallback) - From appsettings.json or environment variables
   - Path: `SignatureCertificate:Path` or `SIGNATURE_CERT_PATH`
   - Password: `SignatureCertificate:Password` or `SIGNATURE_CERT_PASSWORD`

## Security
- Token-based signing: Validates token expiry before signing
- Token invalidation: After successful signing, token cannot be reused
- Certificate security: Temporary uploaded files are deleted after use
");
    }
}
