namespace Contracts.API.ContractsHandler.ValidateContract;

public class ValidateContractEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/{id:guid}/validate", async (
            Guid id,
            HttpRequest request,
            ISender sender,
            ILogger<ValidateContractEndpoint> logger) =>
        {
            try
            {
                if (!request.HasFormContentType || request.Form.Files.Count == 0)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        errorMessage = "No file uploaded. Please upload a contract document (PDF or DOCX)."
                    });
                }

                var file = request.Form.Files[0];
                
                var allowedExtensions = new[] { ".pdf", ".docx", ".doc" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        errorMessage = $"Invalid file type '{fileExtension}'. Only PDF and DOCX files are supported."
                    });
                }
                
                const long maxFileSize = 10 * 1024 * 1024; 
                if (file.Length > maxFileSize)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        errorMessage = $"File size ({file.Length / 1024 / 1024}MB) exceeds maximum allowed size (10MB)."
                    });
                }

                logger.LogInformation("Validating contract {ContractId} with document: {FileName} ({FileSize} bytes)",
                    id, file.FileName, file.Length);
                
                using var stream = file.OpenReadStream();
                
                var query = new ValidateContractQuery(id, stream, file.FileName);
                var result = await sender.Send(query);

                if (!result.Success)
                {
                    logger.LogWarning("Contract validation failed for {ContractId}: {Error}",
                        id, result.ErrorMessage);
                    return Results.BadRequest(result);
                }

                logger.LogInformation("Contract {ContractId} validated successfully: {MatchPercentage}% match",
                    id, result.Summary?.MatchPercentage);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error validating contract {ContractId}", id);
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Contract Validation Error");
            }
        })
        .DisableAntiforgery()
        .WithTags("Contracts")
        .WithName("ValidateContract")
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<ValidateContractResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Validate contract against document");
    }
}
