namespace Contracts.API.ContractsHandler.FillContractTemplate;

public class FillContractTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /api/contracts/template/fill
        app.MapPost("/api/contracts/template/fill",
                async (HttpRequest request, ISender sender, ILogger<FillContractTemplateEndpoint> logger) =>
                {
                    try
                    {
                        // Kiểm tra request có file không
                        if (!request.HasFormContentType || request.Form.Files.Count == 0)
                        {
                            return Results.BadRequest(new
                            {
                                success = false,
                                error = "No template file uploaded"
                            });
                        }

                        var templateFile = request.Form.Files[0];

                        // Validate file extension
                        if (!templateFile.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                        {
                            return Results.BadRequest(new
                            {
                                success = false,
                                error = "Only .docx files are supported"
                            });
                        }

                        // Parse JSON data từ form
                        var dataJson = request.Form["data"].ToString();
                        if (string.IsNullOrEmpty(dataJson))
                        {
                            return Results.BadRequest(new
                            {
                                success = false,
                                error = "Contract data is required"
                            });
                        }

                        // ✅ FIX: Clean up JSON string to handle unescaped newlines
                        // Some clients may send newlines as actual \n characters instead of escaped \\n
                        // This causes JsonException: '0x0A' is invalid within a JSON string
                        logger.LogDebug("Original JSON length: {Length}", dataJson.Length);

                        Dictionary<string, string>? data = null;
                        try
                        {
                            // Try parsing with relaxed options first
                            var options = new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true,
                                AllowTrailingCommas = true,
                                ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
                            };

                            data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(dataJson, options);
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            // If normal parsing fails, try to fix common issues
                            logger.LogWarning(ex, "Failed to parse JSON directly, attempting to clean and retry");

                            try
                            {
                                // Method 1: Try to fix unescaped newlines by pre-processing the JSON
                                // This handles the case where string values contain actual newlines
                                var cleanedJson = CleanJsonString(dataJson);
                                logger.LogDebug("Cleaned JSON length: {Length}", cleanedJson.Length);

                                var options = new System.Text.Json.JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true,
                                    AllowTrailingCommas = true
                                };

                                data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(cleanedJson, options);
                                logger.LogInformation("Successfully parsed JSON after cleaning");
                            }
                            catch (Exception cleanEx)
                            {
                                logger.LogError(cleanEx, "Failed to parse JSON even after cleaning");
                                return Results.BadRequest(new
                                {
                                    success = false,
                                    error = $"Invalid JSON format: {ex.Message}. Please ensure newlines are properly escaped as \\n",
                                    details = "JSON strings must not contain unescaped newlines. Use \\n instead."
                                });
                            }
                        }

                        if (data == null || data.Count == 0)
                        {
                            return Results.BadRequest(new
                            {
                                success = false,
                                error = "Invalid contract data format or empty data"
                            });
                        }

                        logger.LogInformation("Filling template {FileName} with {Count} fields", templateFile.FileName,
                            data.Count);

                        // Tạo output filename
                        var outputFileName = $"HopDong_{DateTime.UtcNow:yyyyMMdd_HHmmss}.docx";

                        // Tạo command
                        using var templateStream = templateFile.OpenReadStream();
                        var command = new FillContractTemplateCommand(
                            TemplateStream: templateStream,
                            Data: data,
                            OutputFileName: outputFileName
                        );

                        var result = await sender.Send(command);

                        if (!result.Success || result.FilledDocumentStream == null)
                        {
                            return Results.BadRequest(new
                            {
                                success = false,
                                error = result.ErrorMessage
                            });
                        }

                        // Trả về file đã điền
                        return Results.File(
                            fileStream: result.FilledDocumentStream,
                            contentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                            fileDownloadName: outputFileName
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error in fill contract template endpoint");
                        return Results.Problem(
                            title: "Fill template failed",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status500InternalServerError
                        );
                    }
                })
            .DisableAntiforgery()
            .WithTags("Contracts")
            .WithName("FillContractTemplate")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Điền thông tin vào template hợp đồng lao động Word");
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
