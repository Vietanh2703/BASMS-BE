using Contracts.API.Extensions;

namespace Contracts.API.ContractsHandler.FillContractTemplate;

/// <summary>
/// Command để điền thông tin vào Word template hợp đồng lao động
/// </summary>
public record FillContractTemplateCommand(
    Stream TemplateStream,
    Dictionary<string, string> Data,
    string OutputFileName
) : ICommand<FillContractTemplateResult>;

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
