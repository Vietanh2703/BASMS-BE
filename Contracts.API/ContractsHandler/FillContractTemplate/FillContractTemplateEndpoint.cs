using Contracts.API.Extensions;
using Dapper;

namespace Contracts.API.ContractsHandler.FillContractTemplate;

/// <summary>
/// Request DTO để điền template từ S3
/// </summary>
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
        // Route: POST /api/contracts/template/fill-from-s3
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

                        // BƯỚC 2: SAU KHI LƯU DOCUMENT VÀO DATABASE, LẤY THÔNG TIN TỪ DATABASE ĐỂ GỬI EMAIL
                        if (result.FilledDocumentId.HasValue)
                        {
                            try
                            {
                                using var connection = await connectionFactory.CreateConnectionAsync();

                                // Query document từ database để lấy thông tin chính xác
                                var savedDocument = await connection.QueryFirstOrDefaultAsync<ContractDocument>(
                                    "SELECT * FROM contract_documents WHERE Id = @Id AND IsDeleted = 0",
                                    new { Id = result.FilledDocumentId.Value });

                                if (savedDocument != null)
                                {
                                    logger.LogInformation("Retrieved saved document from database: {DocumentId}, Email: {Email}",
                                        savedDocument.Id, savedDocument.DocumentEmail ?? "N/A");

                                    // Gửi email nếu có DocumentEmail và SecurityToken
                                    if (!string.IsNullOrEmpty(savedDocument.DocumentEmail) &&
                                        !string.IsNullOrEmpty(savedDocument.Tokens) &&
                                        savedDocument.TokenExpiredDay.HasValue)
                                    {
                                        // Trích xuất contractNumber từ request hoặc data
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

                                        logger.LogInformation("✓ Successfully sent contract signing email to {Email}", savedDocument.DocumentEmail);
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
                                // Không fail toàn bộ request nếu email gửi thất bại
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
            .WithTags("Contracts")
            .WithName("FillContractTemplateFromS3")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Điền template từ S3 và tạo token bảo mật cho ký điện tử");
    }

    /// <summary>
    /// Trích xuất giá trị từ data dictionary theo nhiều key có thể có
    /// </summary>
    private static string? ExtractFromData(Dictionary<string, object>? data, params string[] possibleKeys)
    {
        if (data == null || data.Count == 0)
            return null;

        foreach (var key in possibleKeys)
        {
            // Tìm key trong dictionary (case-insensitive)
            var foundKey = data.Keys.FirstOrDefault(k =>
                k.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                k.Replace("{{", "").Replace("}}", "").Trim().Equals(key, StringComparison.OrdinalIgnoreCase));

            if (foundKey != null && data.TryGetValue(foundKey, out var value))
            {
                if (value is string strValue && !string.IsNullOrWhiteSpace(strValue))
                    return strValue;

                if (value is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var extracted = jsonElement.GetString();
                    if (!string.IsNullOrWhiteSpace(extracted))
                        return extracted;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Clean JSON string by escaping unescaped newlines and other special characters
    /// This fixes the '0x0A' is invalid within a JSON string error
    /// </summary>
    private static string CleanJsonString(string json)
    {
        if (string.IsNullOrEmpty(json))
            return json;

        var result = new System.Text.StringBuilder(json.Length + 100);
        var insideString = false;
        var escaping = false;

        for (int i = 0; i < json.Length; i++)
        {
            var c = json[i];

            // Track if we're inside a string (between quotes)
            if (c == '"' && !escaping)
            {
                insideString = !insideString;
                result.Append(c);
                continue;
            }

            // Track escaping
            if (c == '\\' && insideString && !escaping)
            {
                escaping = true;
                result.Append(c);
                continue;
            }

            // If we're escaping, just append the character as-is
            if (escaping)
            {
                escaping = false;
                result.Append(c);
                continue;
            }

            // If we're inside a string and encounter a newline, escape it
            if (insideString)
            {
                switch (c)
                {
                    case '\n': // Line feed (0x0A)
                        result.Append("\\n");
                        break;
                    case '\r': // Carriage return (0x0D)
                        result.Append("\\r");
                        break;
                    case '\t': // Tab (0x09)
                        result.Append("\\t");
                        break;
                    default:
                        result.Append(c);
                        break;
                }
            }
            else
            {
                // Outside string, append as-is
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
