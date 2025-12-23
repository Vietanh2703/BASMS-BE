namespace Contracts.API.ContractsHandler.FillContractTemplate;

public record FillContractFromS3Request(
    Guid TemplateDocumentId,
    Dictionary<string, object>? Data,
    string? CustomerEmail = null,
    string? CustomerName = null,
    string? ContractNumber = null
);

public class FillContractTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/template/fill-from-s3",
                async (FillContractFromS3Request request, ISender sender, EmailHandler emailHandler, IDbConnectionFactory connectionFactory, ILogger<FillContractTemplateEndpoint> logger) =>
                {
                    try
                    {
                        logger.LogInformation("Filling template from S3: {TemplateId}", request.TemplateDocumentId);

                        var command = new FillContractFromTemplateCommand(
                            TemplateDocumentId: request.TemplateDocumentId,
                            Data: request.Data
                        );

                        var result = await sender.Send(command);

                        if (!result.Success)
                        {
                            return Results.BadRequest(new
                            {
                                success = false,
                                error = result.ErrorMessage
                            });
                        }
                        
                        if (result.FilledDocumentId.HasValue)
                        {
                            try
                            {
                                using var connection = await connectionFactory.CreateConnectionAsync();

                                var savedDocument = await connection.QueryFirstOrDefaultAsync<ContractDocument>(
                                    "SELECT * FROM contract_documents WHERE Id = @Id AND IsDeleted = 0",
                                    new { Id = result.FilledDocumentId.Value });

                                if (savedDocument != null)
                                {
                                    logger.LogInformation("Retrieved saved document from database: {DocumentId}, Email: {Email}",
                                        savedDocument.Id, savedDocument.DocumentEmail ?? "N/A");
                                    
                                    if (!string.IsNullOrEmpty(savedDocument.DocumentEmail) &&
                                        !string.IsNullOrEmpty(savedDocument.Tokens) &&
                                        savedDocument.TokenExpiredDay.HasValue)
                                    {
                                        var contractNumber = request.ContractNumber ??
                                            ExtractFromData(request.Data, "SoHopDong", "ContractNumber", "MaHopDong") ??
                                            "N/A";

                                        logger.LogInformation("Sending contract signing email to {Email} (from database) for document {DocumentId}",
                                            savedDocument.DocumentEmail, savedDocument.Id);

                                        await emailHandler.SendContractSigningEmailAsync(
                                            customerName: savedDocument.DocumentCustomerName ?? "Quý khách",
                                            email: savedDocument.DocumentEmail,
                                            contractNumber: contractNumber,
                                            documentId: savedDocument.Id,
                                            securityToken: savedDocument.Tokens,
                                            tokenExpiredDay: savedDocument.TokenExpiredDay.Value
                                        );

                                        logger.LogInformation("Successfully sent contract signing email to {Email}", savedDocument.DocumentEmail);
                                    }
                                    else
                                    {
                                        logger.LogWarning("Skipping email notification - Missing required info in database: Email={Email}, Token={HasToken}, TokenExpiry={HasExpiry}",
                                            savedDocument.DocumentEmail ?? "NULL",
                                            !string.IsNullOrEmpty(savedDocument.Tokens),
                                            savedDocument.TokenExpiredDay.HasValue);
                                    }
                                }
                                else
                                {
                                    logger.LogWarning("Could not retrieve saved document {DocumentId} from database for email notification",
                                        result.FilledDocumentId.Value);
                                }
                            }
                            catch (Exception emailEx)
                            {
                                logger.LogError(emailEx,
                                    "Failed to send contract signing email for document {DocumentId}. Contract was filled successfully but customer will not receive email notification.",
                                    result.FilledDocumentId);
                            }
                        }

                        return Results.Ok(new
                        {
                            success = true,
                            data = new
                            {
                                documentId = result.FilledDocumentId,
                                fileUrl = result.FilledFileUrl,
                                fileName = result.FilledFileName,
                                folderPath = result.FolderPath,
                                securityToken = result.SecurityToken,
                                tokenExpiredDay = result.TokenExpiredDay,
                                signatureUrl = $"/api/contracts/documents/{result.FilledDocumentId}/sign?token={result.SecurityToken}"
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error filling template from S3");
                        return Results.Problem(
                            title: "Fill template from S3 failed",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status500InternalServerError
                        );
                    }
                })
            .RequireAuthorization()
            .WithTags("Contracts")
            .WithName("FillContractTemplateFromS3")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Điền template từ S3 và tạo token bảo mật cho ký điện tử");
    }


    private static string? ExtractFromData(Dictionary<string, object>? data, params string[] possibleKeys)
    {
        if (data == null || data.Count == 0)
            return null;

        foreach (var key in possibleKeys)
        {
            var foundKey = data.Keys.FirstOrDefault(k =>
                k.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                k.Replace("{{", "").Replace("}}", "").Trim().Equals(key, StringComparison.OrdinalIgnoreCase));

            if (foundKey != null && data.TryGetValue(foundKey, out var value))
            {
                if (value is string strValue && !string.IsNullOrWhiteSpace(strValue))
                    return strValue;

                if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
                {
                    var extracted = jsonElement.GetString();
                    if (!string.IsNullOrWhiteSpace(extracted))
                        return extracted;
                }
            }
        }

        return null;
    }


    private static string CleanJsonString(string json)
    {
        if (string.IsNullOrEmpty(json))
            return json;

        var result = new StringBuilder(json.Length + 100);
        var insideString = false;
        var escaping = false;

        for (int i = 0; i < json.Length; i++)
        {
            var c = json[i];
            
            if (c == '"' && !escaping)
            {
                insideString = !insideString;
                result.Append(c);
                continue;
            }
            
            if (c == '\\' && insideString && !escaping)
            {
                escaping = true;
                result.Append(c);
                continue;
            }
            
            if (escaping)
            {
                escaping = false;
                result.Append(c);
                continue;
            }
            
            if (insideString)
            {
                switch (c)
                {
                    case '\n':
                        result.Append("\\n");
                        break;
                    case '\r':
                        result.Append("\\r");
                        break;
                    case '\t': 
                        result.Append("\\t");
                        break;
                    default:
                        result.Append(c);
                        break;
                }
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
