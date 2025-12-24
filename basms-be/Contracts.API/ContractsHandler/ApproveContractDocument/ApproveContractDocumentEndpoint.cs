namespace Contracts.API.ContractsHandler.ApproveContractDocument;

public class ApproveContractDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/documents/{documentId:guid}/approve", async (
            [FromRoute] Guid documentId,
            [FromBody] ApproveContractDocumentRequest? request,
            ISender sender,
            ILogger<ApproveContractDocumentEndpoint> logger,
            CancellationToken cancellationToken) =>
        {

            var command = new ApproveContractDocumentCommand(
                DocumentId: documentId,
                ApprovedBy: request?.ApprovedBy,
                Notes: request?.Notes
            );

            var result = await sender.Send(command, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to approve contract document {DocumentId}: {Error}",
                    documentId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            logger.LogInformation(
                @"Contract document {DocumentName} approved successfully!
                  - Document Type: {DocumentType}
                  - Version: {Version}
                  - Approved At: {ApprovedAt}",
                result.DocumentName,
                result.DocumentType,
                result.Version,
                result.ApprovedAt);

            return Results.Ok(new
            {
                success = true,
                documentId = result.DocumentId,
                documentName = result.DocumentName,
                documentType = result.DocumentType,
                version = result.Version,
                approvedAt = result.ApprovedAt,
                message = $"Contract document '{result.DocumentName}' has been approved successfully."
            });
        })
        .RequireAuthorization()
        .WithName("ApproveContractDocument")
        .WithTags("Contracts")
        .Produces(200)
        .Produces(400)
        .Produces(404)
        .WithSummary("Approve a contract document");
    }
}

public record ApproveContractDocumentRequest
{
    public Guid? ApprovedBy { get; init; }
    public string? Notes { get; init; }
}
