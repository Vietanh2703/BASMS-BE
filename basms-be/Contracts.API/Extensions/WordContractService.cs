using Xceed.Document.NET;
using Xceed.Words.NET;

namespace Contracts.API.Extensions;

/// <summary>
/// Implementation của Word contract service sử dụng DocX library
/// </summary>
public class WordContractService : IWordContractService
{
    private readonly ILogger<WordContractService> _logger;

    public WordContractService(ILogger<WordContractService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Điền thông tin vào Word template
    /// Placeholders format: {{KEY}} trong Word sẽ được replace bằng value
    /// </summary>
    public async Task<(bool Success, Stream? FileStream, string? ErrorMessage)> FillLaborContractTemplateAsync(
        Stream templateStream,
        Dictionary<string, string> placeholders,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Filling labor contract template with {Count} placeholders", placeholders.Count);

            // Copy template stream to memory để không ảnh hưởng original
            var templateMemoryStream = new MemoryStream();
            await templateStream.CopyToAsync(templateMemoryStream, cancellationToken);
            templateMemoryStream.Position = 0;

            // Load Word document từ stream
            var doc = DocX.Load(templateMemoryStream);

            try
            {
                // Log original text to debug
                _logger.LogInformation("Original document has {Length} characters", doc.Text.Length);
                _logger.LogDebug("First 200 chars: {Text}", doc.Text.Length > 200 ? doc.Text.Substring(0, 200) : doc.Text);

                // Replace tất cả placeholders
                int replacementCount = 0;
                foreach (var placeholder in placeholders)
                {
                    var key = $"{{{{{placeholder.Key}}}}}"; // {{KEY}}
                    var value = placeholder.Value ?? string.Empty;

                    _logger.LogInformation("Attempting to replace '{Key}' with '{Value}'", key, value);

                    // Check if placeholder exists in document
                    if (doc.Text.Contains(key))
                    {
                        doc.ReplaceText(key, value);
                        replacementCount++;
                        _logger.LogInformation("✓ Replaced '{Key}'", key);
                    }
                    else
                    {
                        _logger.LogWarning("Placeholder '{Key}' not found in document", key);
                    }
                }

                _logger.LogInformation("Total replacements made: {Count} out of {Total}", replacementCount, placeholders.Count);

                // Save document vào memory stream
                var outputStream = new MemoryStream();
                doc.SaveAs(outputStream);
                outputStream.Position = 0;

                _logger.LogInformation("✓ Successfully filled labor contract template. Output size: {Size} bytes", outputStream.Length);

                return (true, outputStream, null);
            }
            finally
            {
                // Dispose document
                doc.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fill labor contract template");
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Điền thông tin vào Word template theo label (hỗ trợ format: "Label: ……")
    /// </summary>
    public async Task<(bool Success, Stream? FileStream, string? ErrorMessage)> FillLaborContractByLabelAsync(
        Stream templateStream,
        Dictionary<string, string> labelValues,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Filling labor contract by labels with {Count} values", labelValues.Count);

            // Copy template stream
            var templateMemoryStream = new MemoryStream();
            await templateStream.CopyToAsync(templateMemoryStream, cancellationToken);
            templateMemoryStream.Position = 0;

            // Load document
            var doc = DocX.Load(templateMemoryStream);

            try
            {
                int replacementCount = 0;

                // Duyệt qua tất cả paragraphs
                foreach (var paragraph in doc.Paragraphs)
                {
                    var text = paragraph.Text;

                    // Thay thế theo pattern "Label: ……"
                    foreach (var labelValue in labelValues)
                    {
                        var label = labelValue.Key;
                        var value = labelValue.Value ?? string.Empty;

                        // Pattern: "Họ và tên:………" hoặc "Họ và tên: ………"
                        var patterns = new[]
                        {
                            $"{label}:\\s*[…\\.]+",  // "Label:………" or "Label: ………"
                            $"{label}:[…\\.]+",       // "Label:………"
                        };

                        foreach (var pattern in patterns)
                        {
                            if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern))
                            {
                                var newText = System.Text.RegularExpressions.Regex.Replace(
                                    text,
                                    pattern,
                                    $"{label}: {value}");

                                // Replace trong paragraph
                                paragraph.ReplaceText(text, newText);
                                replacementCount++;

                                _logger.LogInformation("✓ Replaced label '{Label}' with '{Value}'", label, value);
                                break;
                            }
                        }
                    }
                }

                _logger.LogInformation("Total label replacements made: {Count} out of {Total}", replacementCount, labelValues.Count);

                // Save
                var outputStream = new MemoryStream();
                doc.SaveAs(outputStream);
                outputStream.Position = 0;

                _logger.LogInformation("✓ Successfully filled contract by labels. Output size: {Size} bytes", outputStream.Length);

                return (true, outputStream, null);
            }
            finally
            {
                doc.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fill contract by labels");
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Extract toàn bộ text từ Word document
    /// IMPROVED: Extract tất cả nội dung bao gồm tables, headers, footers
    /// Giữ nguyên tất cả ký tự Unicode, whitespace, và cách dòng
    /// </summary>
    public Task<(bool Success, string? Text, string? ErrorMessage)> ExtractTextFromWordAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Extracting text from Word document with full Unicode support");

            using var doc = DocX.Load(documentStream);

            // ✅ IMPROVED: Extract text từ tất cả nguồn
            var textBuilder = new System.Text.StringBuilder();

            // =================================================================
            // 1. EXTRACT HEADERS (Đầu trang)
            // =================================================================
            if (doc.Headers?.Odd?.Paragraphs != null)
            {
                foreach (var header in doc.Headers.Odd.Paragraphs)
                {
                    if (!string.IsNullOrWhiteSpace(header.Text))
                    {
                        textBuilder.AppendLine(header.Text);
                        _logger.LogDebug("Extracted from header: {Text}", header.Text.Substring(0, Math.Min(50, header.Text.Length)));
                    }
                }
            }

            // =================================================================
            // 2. EXTRACT MAIN BODY PARAGRAPHS (Nội dung chính)
            // =================================================================
            if (doc.Paragraphs != null)
            {
                foreach (var paragraph in doc.Paragraphs)
                {
                    // Lấy text từ paragraph
                    // DocX.Paragraph.Text tự động gộp từ các runs và giữ nguyên Unicode
                    var paragraphText = paragraph.Text;

                    // Giữ nguyên paragraph, kể cả khi empty (preserve spacing)
                    if (!string.IsNullOrEmpty(paragraphText))
                    {
                        textBuilder.AppendLine(paragraphText);
                    }
                    else
                    {
                        // Preserve empty lines để giữ nguyên format
                        textBuilder.AppendLine();
                    }
                }
            }
            else
            {
                _logger.LogWarning("Document has no paragraphs");
            }

            // =================================================================
            // 3. EXTRACT TABLES (Bảng)
            // =================================================================
            if (doc.Tables != null && doc.Tables.Count > 0)
            {
                _logger.LogInformation("Found {TableCount} tables in document", doc.Tables.Count);

                foreach (var table in doc.Tables)
                {
                    textBuilder.AppendLine(); // Separator before table
                    textBuilder.AppendLine("=== TABLE ===");

                    foreach (var row in table.Rows)
                    {
                        var rowTexts = new List<string>();
                        foreach (var cell in row.Cells)
                        {
                            // Extract text từ mỗi cell
                            var cellText = string.Join(" ", cell.Paragraphs.Select(p => p.Text));
                            rowTexts.Add(cellText);
                        }
                        // Join cells với tab separator
                        textBuilder.AppendLine(string.Join("\t", rowTexts));
                    }

                    textBuilder.AppendLine("=== END TABLE ===");
                    textBuilder.AppendLine();
                }
            }

            // =================================================================
            // 4. EXTRACT FOOTERS (Chân trang)
            // =================================================================
            if (doc.Footers?.Odd?.Paragraphs != null)
            {
                foreach (var footer in doc.Footers.Odd.Paragraphs)
                {
                    if (!string.IsNullOrWhiteSpace(footer.Text))
                    {
                        textBuilder.AppendLine(footer.Text);
                        _logger.LogDebug("Extracted from footer: {Text}", footer.Text.Substring(0, Math.Min(50, footer.Text.Length)));
                    }
                }
            }

            var text = textBuilder.ToString();

            // Normalize line endings (convert \r\n to \n, then back to \r\n for consistency)
            text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);

            _logger.LogInformation(
                "✓ Successfully extracted {Length} characters from Word document " +
                "({ParagraphCount} paragraphs, {TableCount} tables)",
                text.Length,
                doc.Paragraphs?.Count ?? 0,
                doc.Tables?.Count ?? 0);

            return Task.FromResult<(bool Success, string? Text, string? ErrorMessage)>((true, text, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from Word document");
            return Task.FromResult<(bool Success, string? Text, string? ErrorMessage)>((false, null, ex.Message));
        }
    }
}
