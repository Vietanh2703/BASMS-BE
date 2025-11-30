using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Contracts.API.ContractsHandler.ApproveContractDocument;

/// <summary>
/// Endpoint để approve contract document
/// Director/Manager gọi endpoint này sau khi xem xét và chấp thuận tài liệu hợp đồng
/// </summary>
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
            logger.LogInformation(
                "POST /api/contracts/documents/{DocumentId}/approve - Approving contract document",
                documentId);

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
                @"✓ Contract document {DocumentName} approved successfully!
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
        .WithSummary("Approve a contract document")
        .WithDescription("Updates document type to 'approved_document' and version to 'completed'");
    }
}

/// <summary>
/// Request DTO cho approve contract document
/// </summary>
public record ApproveContractDocumentRequest
{
    /// <summary>
    /// ID của user approve (Director/Manager)
    /// </summary>
    public Guid? ApprovedBy { get; init; }

    /// <summary>
    /// Ghi chú khi approve
    /// </summary>
    public string? Notes { get; init; }
}
