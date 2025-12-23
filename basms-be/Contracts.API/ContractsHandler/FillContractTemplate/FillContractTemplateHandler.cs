namespace Contracts.API.ContractsHandler.FillContractTemplate;


public record FillContractTemplateCommand(
    Stream TemplateStream,
    Dictionary<string, string> Data,
    string OutputFileName
) : ICommand<FillContractTemplateResult>;

public record FillContractFromTemplateCommand(
    Guid TemplateDocumentId,
    Dictionary<string, object>? Data = null
) : ICommand<FillContractFromTemplateResult>;

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

            logger.LogInformation("Successfully filled contract template");

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
            
            var placeholderData = ConvertToStringDictionary(request.Data);
            logger.LogInformation("Converted {Count} placeholders", placeholderData.Count);

            var (folderPath, fileName) = DetermineS3PathAndFileName(templateDoc.DocumentName);
            logger.LogInformation("Target path: {FolderPath}/{FileName}", folderPath, fileName);

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

            var filledDocumentId = Guid.NewGuid();
            var securityToken = Guid.NewGuid().ToString(); 
            var tokenExpiredDay = DateTime.UtcNow.AddDays(7);
            
            string? documentEmail = null;
            string? documentCustomerName = null;
            DateTime? contractStartDate = null;
            DateTime? contractEndDate = null;

            if (request.Data != null)
            {

                logger.LogInformation("Available keys in Data: {Keys}", string.Join(", ", request.Data.Keys));
                
                if (request.Data.TryGetValue("CompanyEmail", out var customerEmailObj))
                    documentEmail = ExtractStringValue(customerEmailObj);
                else if (request.Data.TryGetValue("EmployeeEmail", out var employeeEmailObj))
                    documentEmail = ExtractStringValue(employeeEmailObj);
                
                if (request.Data.TryGetValue("Name", out var customerNameObj))
                    documentCustomerName = ExtractStringValue(customerNameObj);
                else if (request.Data.TryGetValue("EmployeeName", out var employeeNameObj))
                    documentCustomerName = ExtractStringValue(employeeNameObj);

                var startDateKeys = new[] { "ContractStartDate", "StartDate", "contractStartDate", "startDate", "ContractStart" };
                foreach (var key in startDateKeys)
                {
                    if (request.Data.TryGetValue(key, out var startDateObj))
                    {
                        logger.LogInformation("Found StartDate with key: {Key}, Value: {Value}", key, startDateObj);
                        var startDateStr = ExtractStringValue(startDateObj);

                        if (!string.IsNullOrEmpty(startDateStr))
                        {
                            var parsedDate = TryParseMultipleFormats(startDateStr);
                            if (parsedDate.HasValue)
                            {
                                contractStartDate = parsedDate.Value;
                                logger.LogInformation("Successfully parsed StartDate: {Date} from '{Input}'", parsedDate.Value, startDateStr);
                                break;
                            }
                            else
                            {
                                logger.LogWarning("Failed to parse StartDate string: '{DateStr}'", startDateStr);
                            }
                        }
                    }
                }

                var endDateKeys = new[] { "ContractEndDate", "EndDate", "contractEndDate", "endDate", "ContractEnd" };
                foreach (var key in endDateKeys)
                {
                    if (request.Data.TryGetValue(key, out var endDateObj))
                    {
                        logger.LogInformation("Found EndDate with key: {Key}, Value: {Value}", key, endDateObj);
                        var endDateStr = ExtractStringValue(endDateObj);

                        if (!string.IsNullOrEmpty(endDateStr))
                        {
                            var parsedDate = TryParseMultipleFormats(endDateStr);
                            if (parsedDate.HasValue)
                            {
                                contractEndDate = parsedDate.Value;
                                logger.LogInformation("Successfully parsed EndDate: {Date} from '{Input}'", parsedDate.Value, endDateStr);
                                break;
                            }
                            else
                            {
                                logger.LogWarning("Failed to parse EndDate string: '{DateStr}'", endDateStr);
                            }
                        }
                    }
                }
            }

            logger.LogInformation("Extracted info - Email: {Email}, Name: {Name}, StartDate: {StartDate}, EndDate: {EndDate}",
                documentEmail ?? "N/A",
                documentCustomerName ?? "N/A",
                contractStartDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A",
                contractEndDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A");

            var filledDocument = new ContractDocument
            {
                Id = filledDocumentId,
                DocumentName = fileName,
                FileUrl = s3Key, 
                FileSize = 0,
                DocumentType = "filled_contract",
                Category = templateDoc.Category, 
                Version = "pending_signature",
                Tokens = securityToken,
                TokenExpiredDay = tokenExpiredDay,
                UploadedBy = Guid.Empty,
                DocumentEmail = documentEmail,
                DocumentCustomerName = documentCustomerName,
                StartDate = contractStartDate,  
                EndDate = contractEndDate,     
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

    private string? ExtractStringValue(object? value)
    {
        if (value == null) return null;

        if (value is string stringValue)
            return stringValue;

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => jsonElement.GetString(),
                JsonValueKind.Object => ExtractValueFromJsonObject(jsonElement),
                _ => jsonElement.ToString()
            };
        }

        return value.ToString();
    }


    private string ExtractValueFromJsonObject(JsonElement jsonElement)
    {
        if (jsonElement.ValueKind != JsonValueKind.Object)
        {
            return jsonElement.ToString();
        }


        if (jsonElement.TryGetProperty("value", out var valueProperty))
        {
            return valueProperty.ValueKind switch
            {
                JsonValueKind.String => valueProperty.GetString() ?? string.Empty,
                JsonValueKind.Number => valueProperty.GetDecimal().ToString("N0"),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => valueProperty.ToString()
            };
        }
        
        logger.LogWarning("JsonElement Object không có property 'value', trả về ToString(): {Json}", jsonElement.ToString());
        return jsonElement.ToString();
    }

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
            var placeholderKey = key.StartsWith("{{") ? key : $"{{{{{key}}}}}";
            
            string stringValue;
            if (value is DateTime dateValue)
            {
                stringValue = dateValue.ToString("dd/MM/yyyy");
            }
            else if (value is decimal decimalValue)
            {
                stringValue = decimalValue.ToString("N0"); 
            }
            else if (value is JsonElement jsonElement)
            {
                stringValue = jsonElement.ValueKind switch
                {
                    JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
                    JsonValueKind.Number => jsonElement.GetDecimal().ToString("N0"),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Object => ExtractValueFromJsonObject(jsonElement),
                    JsonValueKind.Array => string.Join(", ", jsonElement.EnumerateArray().Select(e => e.ToString())),
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
        
        var documentId = Guid.NewGuid();
        var dateStr = DateTime.Now.ToString("dd_MM_yyyy");
        var fileName = $"FILLED_{documentId}_{templateKey}_{dateStr}.docx";
        var folderPath = $"contracts/filled/{folderName}";

        return (folderPath, fileName);
    }


    private DateTime? TryParseMultipleFormats(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;
        
        var formats = new[]
        {
            "dd/MM/yyyy",      
            "yyyy-MM-dd",      
            "MM/dd/yyyy",      
            "dd-MM-yyyy",     
            "yyyy/MM/dd",     
            "dd/MM/yyyy HH:mm:ss",  
            "yyyy-MM-dd HH:mm:ss",
            "dd-MM-yyyy HH:mm:ss"
        };


        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(
                dateString,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var result))
            {
                return result;
            }
        }
        
        if (DateTime.TryParse(dateString, out var autoResult))
        {
            return autoResult;
        }

        return null;
    }
}
