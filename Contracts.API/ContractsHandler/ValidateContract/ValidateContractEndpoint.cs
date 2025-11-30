namespace Contracts.API.ContractsHandler.ValidateContract;

public class ValidateContractEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: POST /api/contracts/{id}/validate
        app.MapPost("/api/contracts/{id:guid}/validate", async (
            Guid id,
            HttpRequest request,
            ISender sender,
            ILogger<ValidateContractEndpoint> logger) =>
        {
            try
            {
                // Check if request has file
                if (!request.HasFormContentType || request.Form.Files.Count == 0)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        errorMessage = "No file uploaded. Please upload a contract document (PDF or DOCX)."
                    });
                }

                var file = request.Form.Files[0];

                // Validate file type
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

                // Validate file size (max 10MB)
                const long maxFileSize = 10 * 1024 * 1024; // 10MB
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

                // Open file stream
                using var stream = file.OpenReadStream();

                // Send query to handler
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
        .WithSummary("Validate contract against document")
        .WithDescription(@"
Validates contract information in database against the source contract document (PDF/DOCX).
Returns match percentage and detailed list of differences.

**Usage:**
```bash
curl -X POST http://localhost:5000/api/contracts/{id}/validate \
  -F 'file=@contract.pdf'
```

**Response includes:**
- Overall match percentage
- Section-by-section comparison (Contract Info, Locations, Shifts, Working Conditions)
- Detailed differences with severity levels
- Field-by-field comparison results
");
    }
}
