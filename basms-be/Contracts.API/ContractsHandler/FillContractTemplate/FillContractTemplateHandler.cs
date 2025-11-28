using Contracts.API.Extensions;

namespace Contracts.API.ContractsHandler.FillContractTemplate;

/// <summary>
/// Command để điền thông tin vào Word template hợp đồng lao động (Stream-based)
/// </summary>
public record FillContractTemplateCommand(
    Stream TemplateStream,
    Dictionary<string, string> Data,
    string OutputFileName
) : ICommand<FillContractTemplateResult>;

/// <summary>
/// Command để fill template từ S3 với templateDocumentId (S3-based)
/// </summary>
public record FillContractFromTemplateCommand(
    Guid TemplateDocumentId,
    Dictionary<string, object>? Data = null
) : ICommand<FillContractFromTemplateResult>;

/// <summary>
/// Result cho FillContractFromTemplateCommand
/// </summary>
public record FillContractFromTemplateResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? FilledDocumentId { get; init; }
    public string? FilledFileUrl { get; init; }
    public string? FilledFileName { get; init; }
    public string? FolderPath { get; init; }
    public string? SecurityToken { get; init; }
    public DateTime? TokenExpiredDay { get; init; }
}

/// <summary>
/// Result của việc điền template
/// </summary>
public record FillContractTemplateResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Stream? FilledDocumentStream { get; init; }
    public string? FileName { get; init; }
}

internal class FillContractTemplateHandler(
    IWordContractService wordService,
    ILogger<FillContractTemplateHandler> logger)
    : ICommandHandler<FillContractTemplateCommand, FillContractTemplateResult>
{
    public async Task<FillContractTemplateResult> Handle(
        FillContractTemplateCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Filling contract template with {Count} placeholders for output: {FileName}",
                request.Data.Count,
                request.OutputFileName);

            // Điền thông tin vào template
            var (success, filledStream, error) = await wordService.FillLaborContractTemplateAsync(
                request.TemplateStream,
                request.Data,
                cancellationToken);

            if (!success || filledStream == null)
            {
                return new FillContractTemplateResult
                {
                    Success = false,
                    ErrorMessage = error ?? "Failed to fill contract template"
                };
            }

            logger.LogInformation("✓ Successfully filled contract template");

            return new FillContractTemplateResult
            {
                Success = true,
                FilledDocumentStream = filledStream,
                FileName = request.OutputFileName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fill contract template");
            return new FillContractTemplateResult
            {
                Success = false,
                ErrorMessage = $"Fill template failed: {ex.Message}"
            };
        }
    }
}

/// <summary>
/// Handler để fill template từ S3 - Wrapper cho FillContractTemplateHandler
/// </summary>
internal class FillContractFromTemplateHandler(
    IDbConnectionFactory connectionFactory,
    IS3Service s3Service,
    ISender mediator,
    ILogger<FillContractFromTemplateHandler> logger)
    : ICommandHandler<FillContractFromTemplateCommand, FillContractFromTemplateResult>
{
    public async Task<FillContractFromTemplateResult> Handle(
        FillContractFromTemplateCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Filling template from S3 - TemplateDocumentId: {TemplateId}",
                request.TemplateDocumentId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // BƯỚC 1: LẤY TEMPLATE DOCUMENT TỪ DATABASE
            var templateDoc = await connection.QueryFirstOrDefaultAsync<ContractDocument>(
                "SELECT * FROM contract_documents WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.TemplateDocumentId });

            if (templateDoc == null)
            {
                return new FillContractFromTemplateResult
                {
                    Success = false,
                    ErrorMessage = $"Template document {request.TemplateDocumentId} not found"
                };
            }

            logger.LogInformation("Template: {TemplateName}", templateDoc.DocumentName);

            // BƯỚC 2: DOWNLOAD TEMPLATE TỪ S3
            var (downloadSuccess, templateStream, downloadError) = await s3Service.DownloadFileAsync(
                templateDoc.FileUrl,
                cancellationToken);

            if (!downloadSuccess || templateStream == null)
            {
                return new FillContractFromTemplateResult
                {
                    Success = false,
                    ErrorMessage = downloadError ?? "Failed to download template from S3"
                };
            }

            logger.LogInformation("Downloaded template from S3: {FileUrl}", templateDoc.FileUrl);

            // BƯỚC 3: CONVERT DATA TỪ Dictionary<string, object> SANG Dictionary<string, string>
            var placeholderData = ConvertToStringDictionary(request.Data);
            logger.LogInformation("Converted {Count} placeholders", placeholderData.Count);

            // BƯỚC 4: XÁC ĐỊNH OUTPUT FILENAME VÀ FOLDER
            var (folderPath, fileName) = DetermineS3PathAndFileName(templateDoc.DocumentName);
            logger.LogInformation("Target path: {FolderPath}/{FileName}", folderPath, fileName);

            // BƯỚC 5: GỌI FillContractTemplateHandler ĐỂ FILL TEMPLATE
            var fillCommand = new FillContractTemplateCommand(
                TemplateStream: templateStream,
                Data: placeholderData,
                OutputFileName: fileName
            );

            var fillResult = await mediator.Send(fillCommand, cancellationToken);
            templateStream.Dispose();

            if (!fillResult.Success || fillResult.FilledDocumentStream == null)
            {
                return new FillContractFromTemplateResult
                {
                    Success = false,
                    ErrorMessage = fillResult.ErrorMessage ?? "Failed to fill template"
                };
            }

            logger.LogInformation("Successfully filled template");

            // BƯỚC 6: UPLOAD FILLED FILE LÊN S3
            var s3Key = $"{folderPath}/{fileName}";
            var (uploadSuccess, s3Url, uploadError) = await s3Service.UploadFileWithCustomKeyAsync(
                fillResult.FilledDocumentStream,
                s3Key,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                cancellationToken);

            fillResult.FilledDocumentStream.Dispose();

            if (!uploadSuccess || string.IsNullOrEmpty(s3Url))
            {
                return new FillContractFromTemplateResult
                {
                    Success = false,
                    ErrorMessage = uploadError ?? "Failed to upload filled contract to S3"
                };
            }

            logger.LogInformation("Uploaded filled contract to S3: {S3Url}", s3Url);

            // BƯỚC 7: TẠO RECORD TRONG CONTRACT_DOCUMENTS VỚI TOKEN BẢO MẬT
            var filledDocumentId = Guid.NewGuid();
            var securityToken = Guid.NewGuid().ToString(); // Token bảo mật để truy cập tài liệu
            var tokenExpiredDay = DateTime.UtcNow.AddDays(7); // Token hết hạn sau 7 ngày

            // Extract customer info và contract dates từ data để lưu vào document
            string? documentEmail = null;
            string? documentCustomerName = null;
            DateTime? contractStartDate = null;
            DateTime? contractEndDate = null;

            if (request.Data != null)
            {
                // Tìm email: CustomerEmail hoặc EmployeeEmail
                if (request.Data.TryGetValue("CompanyEmail", out var customerEmailObj))
                    documentEmail = ExtractStringValue(customerEmailObj);
                else if (request.Data.TryGetValue("EmployeeEmail", out var employeeEmailObj))
                    documentEmail = ExtractStringValue(employeeEmailObj);

                // Tìm name: CustomerName hoặc EmployeeName
                if (request.Data.TryGetValue("Name", out var customerNameObj))
                    documentCustomerName = ExtractStringValue(customerNameObj);
                else if (request.Data.TryGetValue("EmployeeName", out var employeeNameObj))
                    documentCustomerName = ExtractStringValue(employeeNameObj);

                // Tìm contract dates: ContractStartDate và ContractEndDate
                if (request.Data.TryGetValue("ContractStartDate", out var startDateObj))
                {
                    var startDateStr = ExtractStringValue(startDateObj);
                    if (DateTime.TryParse(startDateStr, out var parsedStartDate))
                        contractStartDate = parsedStartDate;
                }

                if (request.Data.TryGetValue("ContractEndDate", out var endDateObj))
                {
                    var endDateStr = ExtractStringValue(endDateObj);
                    if (DateTime.TryParse(endDateStr, out var parsedEndDate))
                        contractEndDate = parsedEndDate;
                }
            }

            logger.LogInformation("Extracted info - Email: {Email}, Name: {Name}, StartDate: {StartDate}, EndDate: {EndDate}",
                documentEmail ?? "N/A",
                documentCustomerName ?? "N/A",
                contractStartDate?.ToString("yyyy-MM-dd") ?? "N/A",
                contractEndDate?.ToString("yyyy-MM-dd") ?? "N/A");

            var filledDocument = new ContractDocument
            {
                Id = filledDocumentId,
                DocumentName = fileName,
                FileUrl = s3Key,  // LƯU S3 KEY
                FileSize = 0,
                DocumentType = "filled_contract",
                Category = templateDoc.Category,  // Tự động điền category từ template
                Version = "pending_signature",
                Tokens = securityToken,
                TokenExpiredDay = tokenExpiredDay,
                UploadedBy = Guid.Empty,
                DocumentEmail = documentEmail,
                DocumentCustomerName = documentCustomerName,
                StartDate = contractStartDate,  // Ngày bắt đầu hợp đồng
                EndDate = contractEndDate,       // Ngày kết thúc hợp đồng
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            await connection.InsertAsync(filledDocument);
            logger.LogInformation(
                "Created ContractDocument record: {DocumentId} with S3 key: {S3Key}, Token: {Token}, Expires: {ExpiryDate}",
                filledDocumentId, s3Key, securityToken, tokenExpiredDay);

            return new FillContractFromTemplateResult
            {
                Success = true,
                FilledFileUrl = s3Url,
                FilledFileName = fileName,
                FolderPath = folderPath,
                FilledDocumentId = filledDocumentId,
                SecurityToken = securityToken,
                TokenExpiredDay = tokenExpiredDay
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error filling template from S3");
            return new FillContractFromTemplateResult
            {
                Success = false,
                ErrorMessage = $"Fill template failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Extract string value từ object (có thể là string, JsonElement, hoặc object khác)
    /// </summary>
    private string? ExtractStringValue(object? value)
    {
        if (value == null) return null;

        if (value is string stringValue)
            return stringValue;

        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                System.Text.Json.JsonValueKind.Object => ExtractValueFromJsonObject(jsonElement),
                _ => jsonElement.ToString()
            };
        }

        return value.ToString();
    }

    /// <summary>
    /// Extract giá trị từ JsonElement Object
    /// Nếu có property "value", lấy giá trị đó
    /// Nếu không, trả về ToString() của object
    /// </summary>
    private string ExtractValueFromJsonObject(System.Text.Json.JsonElement jsonElement)
    {
        if (jsonElement.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return jsonElement.ToString();
        }

        // Kiểm tra xem có property "value" không
        if (jsonElement.TryGetProperty("value", out var valueProperty))
        {
            return valueProperty.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => valueProperty.GetString() ?? string.Empty,
                System.Text.Json.JsonValueKind.Number => valueProperty.GetDecimal().ToString("N0"),
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                _ => valueProperty.ToString()
            };
        }

        // Nếu không có "value", trả về toString của toàn bộ object (fallback)
        logger.LogWarning("JsonElement Object không có property 'value', trả về ToString(): {Json}", jsonElement.ToString());
        return jsonElement.ToString();
    }

    /// <summary>
    /// Convert Dictionary<string, object> sang Dictionary<string, string>
    /// Không hard-code, chỉ convert những gì user cung cấp
    /// Giữ nguyên casing của key từ user
    /// </summary>
    private Dictionary<string, string> ConvertToStringDictionary(Dictionary<string, object>? data)
    {
        var result = new Dictionary<string, string>();

        if (data == null || data.Count == 0)
        {
            logger.LogInformation("No data provided, template will use default values or empty placeholders");
            return result;
        }

        foreach (var (key, value) in data)
        {
            // Tự động thêm {{ }} nếu chưa có - GIỮ NGUYÊN CASING
            var placeholderKey = key.StartsWith("{{") ? key : $"{{{{{key}}}}}";

            // Format giá trị dựa trên type
            string stringValue;
            if (value is DateTime dateValue)
            {
                stringValue = dateValue.ToString("dd/MM/yyyy");
            }
            else if (value is decimal decimalValue)
            {
                stringValue = decimalValue.ToString("N0"); // Format số với dấu phẩy
            }
            else if (value is System.Text.Json.JsonElement jsonElement)
            {
                // Xử lý JsonElement từ ASP.NET Core deserialization
                stringValue = jsonElement.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
                    System.Text.Json.JsonValueKind.Number => jsonElement.GetDecimal().ToString("N0"),
                    System.Text.Json.JsonValueKind.True => "true",
                    System.Text.Json.JsonValueKind.False => "false",
                    System.Text.Json.JsonValueKind.Object => ExtractValueFromJsonObject(jsonElement),
                    System.Text.Json.JsonValueKind.Array => string.Join(", ", jsonElement.EnumerateArray().Select(e => e.ToString())),
                    _ => jsonElement.ToString()
                };
            }
            else
            {
                stringValue = value?.ToString() ?? string.Empty;
            }

            result[placeholderKey] = stringValue;

            logger.LogInformation("Mapping placeholder: {Key} = '{Value}'", placeholderKey, stringValue);
        }

        logger.LogInformation("Converted {Count} placeholders from data", result.Count);
        return result;
    }

    /// <summary>
    /// Xác định folder path và file name (với documentId để tránh conflict)
    /// </summary>
    private (string folderPath, string fileName) DetermineS3PathAndFileName(string templateName)
    {
        var templateKey = Path.GetFileNameWithoutExtension(templateName)
            .Replace("-", "_")
            .ToUpper();

        var folderName = "Hợp đồng khác";

        if (templateKey.Contains("HOP_DONG_DICH_VU_BAO_VE") || templateKey.Contains("DICH_VU"))
        {
            folderName = "Hợp đồng dịch vụ bảo vệ";
        }
        else if (templateKey.Contains("HOP_DONG_LAO_DONG_QUAN_LY") || templateKey.Contains("QUAN_LY"))
        {
            folderName = "Hợp đồng lao động quản lý";
        }
        else if (templateKey.Contains("HOP_DONG_LAO_DONG_NV_BAO_VE") || templateKey.Contains("NHAN_VIEN"))
        {
            folderName = "Hợp đồng lao động nhân viên bảo vệ";
        }

        // Thêm documentId vào tên file để tránh conflict khi xử lí nhiều hợp đồng cùng 1 ngày
        var documentId = Guid.NewGuid();
        var dateStr = DateTime.Now.ToString("dd_MM_yyyy");
        var fileName = $"FILLED_{documentId}_{templateKey}_{dateStr}.docx";
        var folderPath = $"contracts/filled/{folderName}";

        return (folderPath, fileName);
    }
}
