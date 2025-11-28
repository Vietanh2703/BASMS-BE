using BuildingBlocks.CQRS;
using Contracts.API.Data;
using Contracts.API.Models;
using Dapper;
using Dapper.Contrib.Extensions;

namespace Contracts.API.ContractsHandler.ApproveContractDocument;

/// <summary>
/// Handler để approve contract document
/// Update DocumentType thành "approved_document" và Version thành "completed"
/// </summary>
public class ApproveContractDocumentHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ApproveContractDocumentHandler> logger)
    : ICommandHandler<ApproveContractDocumentCommand, ApproveContractDocumentResult>
{
    public async Task<ApproveContractDocumentResult> Handle(
        ApproveContractDocumentCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Approving contract document {DocumentId}",
            request.DocumentId);

        using var connection = await connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // ================================================================
            // BƯỚC 1: LẤY CONTRACT DOCUMENT VÀ VALIDATE
            // ================================================================
            var document = await connection.QueryFirstOrDefaultAsync<ContractDocument>(
                "SELECT * FROM contract_documents WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.DocumentId },
                transaction);

            if (document == null)
            {
                return new ApproveContractDocumentResult
                {
                    Success = false,
                    ErrorMessage = $"Contract document {request.DocumentId} not found"
                };
            }

            logger.LogInformation(
                "Contract document {DocumentName} found - Current type: {DocumentType}, version: {Version}",
                document.DocumentName,
                document.DocumentType,
                document.Version);

            // Check if already approved
            if (document.DocumentType == "approved_document" && document.Version == "completed")
            {
                return new ApproveContractDocumentResult
                {
                    Success = false,
                    ErrorMessage = $"Contract document '{document.DocumentName}' is already approved"
                };
            }

            // ================================================================
            // BƯỚC 2: UPDATE DOCUMENT TYPE, VERSION, APPROVED INFO
            // ================================================================
            var previousType = document.DocumentType;
            var previousVersion = document.Version;
            var vietnamTime = Contracts.API.Extensions.DateTimeExtensions.GetVietnamTime();

            document.DocumentType = "approved_document";
            document.Version = "completed";
            document.ApprovedAt = vietnamTime;  // Thời gian approve theo giờ Việt Nam (UTC+7)
            document.ApprovedBy = request.ApprovedBy;  // User ID của người approve

            await connection.UpdateAsync(document, transaction);

            logger.LogInformation(
                @"✓ Contract document updated successfully:
                  - Document: {DocumentName}
                  - Type: {PreviousType} → {NewType}
                  - Version: {PreviousVersion} → {NewVersion}
                  - Approved At: {ApprovedAt} (Vietnam Time UTC+7)
                  - Approved By: {ApprovedBy}",
                document.DocumentName,
                previousType,
                document.DocumentType,
                previousVersion,
                document.Version,
                vietnamTime,
                request.ApprovedBy);

            // ================================================================
            // BƯỚC 3: COMMIT TRANSACTION
            // ================================================================
            transaction.Commit();

            logger.LogInformation(
                "✓✓✓ Contract document '{DocumentName}' approved successfully!",
                document.DocumentName);

            return new ApproveContractDocumentResult
            {
                Success = true,
                DocumentId = document.Id,
                DocumentName = document.DocumentName,
                DocumentType = document.DocumentType,
                Version = document.Version,
                ApprovedAt = vietnamTime  // Trả về Vietnam time
            };
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            logger.LogError(ex,
                "Failed to approve contract document {DocumentId}",
                request.DocumentId);

            return new ApproveContractDocumentResult
            {
                Success = false,
                ErrorMessage = $"Approval failed: {ex.Message}"
            };
        }
    }
}
