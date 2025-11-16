namespace Contracts.API.Extensions;

/// <summary>
/// Service để xử lý Word document: điền thông tin vào template hợp đồng
/// </summary>
public interface IWordContractService
{
    /// <summary>
    /// Điền thông tin vào Word template và trả về file stream
    /// Hỗ trợ format: {{KEY}}
    /// </summary>
    Task<(bool Success, Stream? FileStream, string? ErrorMessage)> FillLaborContractTemplateAsync(
        Stream templateStream,
        Dictionary<string, string> placeholders,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Điền thông tin vào Word template theo vị trí label
    /// Hỗ trợ format: "Label: ……………"
    /// </summary>
    Task<(bool Success, Stream? FileStream, string? ErrorMessage)> FillLaborContractByLabelAsync(
        Stream templateStream,
        Dictionary<string, string> labelValues,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract text từ Word document
    /// </summary>
    Task<(bool Success, string? Text, string? ErrorMessage)> ExtractTextFromWordAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default);
}
