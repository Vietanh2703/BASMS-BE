using Microsoft.AspNetCore.Mvc;
using Contracts.API.ContractsHandler.FillContractTemplate;

namespace Contracts.API.BackgroundJobs;

public class ProcessContractEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // ============================================
        // ENDPOINT: Trigger Contract Processing (với file PFX upload)
        // ============================================
        app.MapPost("/api/contracts/process-auto",
            async (HttpRequest httpRequest,
                ContractProcessingJob job,
                ILogger<ProcessContractEndpoint> logger) =>
            {
                try
                {
                    // Parse multipart form data
                    if (!httpRequest.HasFormContentType)
                    {
                        return Results.BadRequest(new { success = false, error = "Request must be multipart/form-data" });
                    }

                    var form = await httpRequest.ReadFormAsync();

                    // Get templateDocumentId
                    if (!Guid.TryParse(form["templateDocumentId"], out var templateDocumentId))
                    {
                        return Results.BadRequest(new { success = false, error = "templateDocumentId is required" });
                    }

                    // Get data JSON
                    var dataJson = form["data"].ToString();
                    Dictionary<string, object>? data = null;
                    if (!string.IsNullOrEmpty(dataJson))
                    {
                        try
                        {
                            // Parse JSON với options để handle escape characters
                            var jsonOptions = new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true,
                                AllowTrailingCommas = true
                            };

                            data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(dataJson, jsonOptions);

                            logger.LogInformation("✓ Parsed {Count} data fields from JSON", data?.Count ?? 0);
                        }
                        catch (System.Text.Json.JsonException jsonEx)
                        {
                            // Nếu lỗi do newline characters, thử parse lại bằng Newtonsoft.Json (more lenient)
                            if (jsonEx.Message.Contains("0x0A") || jsonEx.Message.Contains("newline"))
                            {
                                logger.LogWarning("JSON contains unescaped newlines, attempting to parse with Newtonsoft.Json...");

                                try
                                {
                                    // Newtonsoft.Json có thể handle malformed JSON tốt hơn
                                    var newtonsoftData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(dataJson);

                                    if (newtonsoftData != null)
                                    {
                                        // Convert Newtonsoft types to System.Text.Json compatible types
                                        data = new Dictionary<string, object>();
                                        foreach (var kvp in newtonsoftData)
                                        {
                                            // Convert JToken to primitive types
                                            data[kvp.Key] = kvp.Value switch
                                            {
                                                Newtonsoft.Json.Linq.JValue jValue => jValue.Value ?? string.Empty,
                                                Newtonsoft.Json.Linq.JArray jArray => jArray.ToString(),
                                                Newtonsoft.Json.Linq.JObject jObject => jObject.ToString(),
                                                _ => kvp.Value?.ToString() ?? string.Empty
                                            };
                                        }

                                        logger.LogInformation("✓ Successfully parsed {Count} fields using Newtonsoft.Json (fallback)", data.Count);
                                    }
                                    else
                                    {
                                        throw new Exception("Newtonsoft.Json returned null");
                                    }
                                }
                                catch (Exception newtonsoftEx)
                                {
                                    logger.LogError(newtonsoftEx, "Newtonsoft.Json also failed to parse");

                                    return Results.BadRequest(new
                                    {
                                        success = false,
                                        error = "Invalid JSON format in 'data' field - contains unescaped newline characters",
                                        details = jsonEx.Message,
                                        hint = "Frontend must properly escape special characters. Use JSON.stringify() to ensure valid JSON.",
                                        example = "Correct: {\"field\": \"line1\\nline2\"}, Wrong: {\"field\": \"line1\n line2\"}"
                                    });
                                }
                            }
                            else
                            {
                                logger.LogError(jsonEx, "Failed to parse JSON data at position {Position}",
                                    $"Line {jsonEx.LineNumber}, Pos {jsonEx.BytePositionInLine}");

                                return Results.BadRequest(new
                                {
                                    success = false,
                                    error = "Invalid JSON format in 'data' field",
                                    details = jsonEx.Message,
                                    position = $"Line {jsonEx.LineNumber}, Position {jsonEx.BytePositionInLine}",
                                    hint = "Make sure your JSON is properly formatted. Frontend should use JSON.stringify() and ensure all strings are properly escaped."
                                });
                            }
                        }
                    }

                    // Get certificate file (optional)
                    IFormFile? certificateFile = form.Files.GetFile("certificateFile");
                    var certificatePassword = form["certificatePassword"].ToString();

                    logger.LogInformation("Triggering contract processing for template: {TemplateId}", templateDocumentId);

                    if (certificateFile != null)
                    {
                        logger.LogInformation("Certificate file provided: {FileName} ({Size} bytes)",
                            certificateFile.FileName, certificateFile.Length);
                    }

                    // Gọi background job với certificate
                    var result = await job.ProcessContractAsync(
                        templateDocumentId,
                        data,
                        certificateFile,
                        certificatePassword
                    );

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
                        contractId = result.ContractId,
                        contractNumber = result.ContractNumber,
                        filledFileUrl = result.FilledFileUrl,
                        signedFileUrl = result.SignedFileUrl,
                        filledDocumentId = result.FilledDocumentId,
                        signedDocumentId = result.SignedDocumentId,
                        signatureCount = result.SignatureCount,
                        message = result.Message
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing contract");
                    return Results.Problem(
                        title: "Contract processing failed",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            })
            .WithTags("Contracts - Automation")
            .WithName("ProcessContractAuto")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Tự động xử lý contract: Fill → Sign → Import")
            .WithDescription(@"
**Tự động điền placeholders vào template từ JSON data linh hoạt**

Endpoint này nhận JSON data tùy biến và tự động điền vào template Word.
Các key trong data sẽ được convert thành placeholders ({{key}}).

**Request - Hợp đồng dịch vụ:**
```json
{
  ""templateDocumentId"": ""guid-of-service-contract-template"",
  ""data"": {
    ""customerName"": ""Công ty ABC"",
    ""customerAddress"": ""123 Đường XYZ"",
    ""contactPersonName"": ""Nguyễn Văn A"",
    ""contractNumber"": ""CTR-2025-001"",
    ""startDate"": ""2025-01-01"",
    ""endDate"": ""2026-01-01"",
    ""totalValue"": ""500000000""
  }
}
```

**Request - Hợp đồng lao động nhân viên:**
```json
{
  ""templateDocumentId"": ""guid-of-employee-contract-template"",
  ""data"": {
    ""employeeName"": ""Trần Văn B"",
    ""employeeEmail"": ""tranvanb@email.com"",
    ""employeePhone"": ""0912345678"",
    ""employeeAddress"": ""456 Đường ABC"",
    ""identityNumber"": ""001234567890"",
    ""position"": ""Nhân viên bảo vệ"",
    ""salary"": ""8000000"",
    ""contractNumber"": ""HDLD-2025-001"",
    ""startDate"": ""2025-01-01"",
    ""endDate"": ""2026-01-01""
  }
}
```

**Lưu ý:**
- Key names phải khớp với placeholders trong template (VD: ""employeeEmail"" → {{employeeEmail}})
- DateTime sẽ được format thành dd/MM/yyyy tự động
- Decimal/số sẽ được format với dấu phẩy (VD: 8000000 → 8,000,000)
- Không cần cung cấp tất cả placeholders - những placeholder không có data sẽ giữ nguyên trong template

**Response:**
```json
{
  ""success"": true,
  ""contractId"": ""guid"",
  ""contractNumber"": ""CTR-2025-001"",
  ""filledFileUrl"": ""s3://bucket/contracts/filled/FILLED_XXX.docx"",
  ""signedFileUrl"": ""s3://bucket/contracts/signed/SIGNED_XXX.docx"",
  ""signatureCount"": 1,
  ""message"": ""Contract processing completed successfully""
}
```

**Advantages over Lambda:**
- ✅ Không cần deploy riêng
- ✅ Không cần ECR/Docker
- ✅ Test ngay trên localhost
- ✅ Debug dễ dàng
- ✅ Logs rõ ràng
- ✅ Không tốn tiền Lambda
");
    }
}

public record ProcessContractRequest
{
    public Guid TemplateDocumentId { get; init; }
    public Dictionary<string, object>? Data { get; init; }
}
