namespace Contracts.API.Extensions;


public interface IWordContractService
{

    Task<(bool Success, Stream? FileStream, string? ErrorMessage)> FillLaborContractTemplateAsync(
        Stream templateStream,
        Dictionary<string, string> placeholders,
        CancellationToken cancellationToken = default);


    Task<(bool Success, Stream? FileStream, string? ErrorMessage)> FillLaborContractByLabelAsync(
        Stream templateStream,
        Dictionary<string, string> labelValues,
        CancellationToken cancellationToken = default);


    Task<(bool Success, string? Text, string? ErrorMessage)> ExtractTextFromWordAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default);
    
    Task<(bool Success, Stream? FileStream, string? ErrorMessage)> InsertSignatureImageAsync(
        Stream documentStream,
        string contentControlTag,
        Stream imageStream,
        string imageFileName,
        CancellationToken cancellationToken = default);
}
