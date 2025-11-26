using Contracts.API.Extensions;

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
                async (FillContractFromS3Request request, ISender sender, EmailHandler emailHandler, ILogger<FillContractTemplateEndpoint> logger) =>
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

                        // Gửi email ký hợp đồng điện tử nếu có thông tin customer
                        if (!string.IsNullOrEmpty(request.CustomerEmail) && result.FilledDocumentId.HasValue && !string.IsNullOrEmpty(result.SecurityToken))
                        {
                            try
                            {
                                logger.LogInformation("Sending contract signing email to {Email}", request.CustomerEmail);

                                await emailHandler.SendContractSigningEmailAsync(
                                    customerName: request.CustomerName ?? "Quý khách",
                                    email: request.CustomerEmail,
                                    contractNumber: request.ContractNumber ?? "N/A",
                                    documentId: result.FilledDocumentId.Value,
                                    securityToken: result.SecurityToken,
                                    tokenExpiredDay: result.TokenExpiredDay ?? DateTime.UtcNow.AddDays(7)
                                );

                                logger.LogInformation("Contract signing email sent successfully to {Email}", request.CustomerEmail);
                            }
                            catch (Exception emailEx)
                            {
                                logger.LogError(emailEx, "Failed to send contract signing email to {Email}", request.CustomerEmail);
                                // Không fail toàn bộ request nếu email gửi thất bại
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
